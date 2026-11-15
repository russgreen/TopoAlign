using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments.Civil;
using TopoAlign.Commands.Shared;
using TopoAlign.Alignments;
using TopoAlign.Services;
using TopoAlign.Sweep.Road;
using TopoAlign.ViewModels.AlignmentTools;
using TopoAlign.Views.AlignmentTools;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandTJunction : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandTJunction>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandTJunction));

        CommandFlow.Initialise(commandData);

        if (!CommandFlow.AlignmentFeaturesSupported(AlignmentFeature.Read | AlignmentFeature.Create | AlignmentFeature.Update, "T-Junction"))
        {
            return Result.Cancelled;
        }

        using var viewModel = new TJunctionViewModel();

        // alignments that are already selected are used without asking: the first is the main road, the second the side road
        var preselected = 0;
        CommandFlow.UseSelectedElement(commandData.Application.ActiveUIDocument, new AlignmentPickFilter(), element =>
        {
            var accepted = preselected == 0 ? viewModel.SelectMain(element) : viewModel.SelectSide(element);
            if (accepted)
            {
                preselected++;
            }

            return preselected >= 2;
        });

        var view = new TJunctionView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
        // then the same dialog is shown again.
        var dialogResult = CommandFlow.ShowDialogWithPicks(view, () => viewModel.PickRequested = false, () => viewModel.PickRequested, viewModel.RunPendingPick);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Write after the dialog has closed so the transactions run in the Revit API context.
        var document = App.RevitDocument;
        using var group = new TransactionGroup(document, "T-Junction");
        group.Start();
        var stage = "starting";

        try
        {
            var fitting = new CutOutcome();
            var surfaceMade = false;

            // Updating the side alignment rewrites it, which can leave the element objects held from before stale, so the
            // alignments are fetched again by their unique ids once that is done.
            var sideUniqueId = request.Side.UniqueId;
            var mainUniqueId = request.Main?.UniqueId;

            using (var progress = ProgressService.Start("T-Junction", cancelEnabled: false))
            {
                progress.Report("Trimming the side road...");
                stage = "trimming the side road alignment";
                AlignmentApi.Update(document, request.Side, request.SideJson, request.SidePlanLength);

                stage = "replacing the kerb return alignments";

                // a kerb return left by an earlier run of the same junction is replaced, not duplicated
                var kerbNames = new HashSet<string>(request.KerbReturns.Select(k => k.Name));
                var earlier = AlignmentApi.GetAlignmentElements(document)
                    .Where(e => AlignmentApi.GetInfo(e)?.Name is { } name && kerbNames.Contains(name))
                    .Select(e => e.Id)
                    .ToList();
                if (earlier.Count > 0)
                {
                    using var delete = new Transaction(document, "Remove earlier kerb returns");
                    delete.Start();
                    document.Delete(earlier);
                    delete.Commit();
                }

                foreach (var kerb in request.KerbReturns)
                {
                    progress.Report($"Creating '{kerb.Name}'...");
                    AlignmentApi.Create(document, kerb.Name, kerb.Json, kerb.PlanLength);
                }

                var side = document.GetElement(sideUniqueId) ?? throw new InvalidOperationException("The side road alignment is no longer in the model.");
                var main = mainUniqueId is null ? null : document.GetElement(mainUniqueId);

                // Fit the roads that already exist to the junction. Road parts that are family instances are cut with void
                // families (which the user can edit or delete); parts that are direct shapes have their geometry changed.
                if (request.CutPolygon is not null || request.SideJoin is not null || request.SurfaceOutline is not null)
                {
                    stage = "finding the road parts";
                    var mainRoads = request.CutPolygon is not null && main is not null ? RoadBuilder.FindRoads(document, main) : new List<Element>();
                    var sideRoads = request.SideJoin is not null ? RoadBuilder.FindRoads(document, side) : new List<Element>();

                    // direct shapes are fetched again by id after the families are reloaded
                    var mainShapeIds = mainRoads.OfType<DirectShape>().Select(r => r.Id).ToList();
                    var sideShapeIds = sideRoads.OfType<DirectShape>().Select(r => r.Id).ToList();

                    // the voids go into the part families, which are edited and reloaded outside any transaction
                    var voidFamilies = new List<string>();
                    var cutsRoads = request.CutPolygon is not null || request.SideJoin is not null;
                    if (cutsRoads)
                    {
                        progress.Report("Putting the voids into the road families...");
                        stage = "putting the voids into the road families";
                        voidFamilies = JunctionVoids.Apply(document, request.Key, mainRoads.OfType<FamilyInstance>().ToList(), request.CutPolygon,
                            sideRoads.OfType<FamilyInstance>().ToList(), request.SideJoin, fitting);
                    }

                    {
                        stage = "recording the junction";
                        side = document.GetElement(sideUniqueId) ?? side;
                        using var transaction = new Transaction(document, "Fit roads to junction");
                        transaction.Start();

                        if (cutsRoads)
                        {
                            JunctionVoids.SaveRecord(document, request.Key, voidFamilies);
                        }

                        if (request.CutPolygon is { } polygon)
                        {
                            progress.Report("Cutting the junction out of the main road...");
                            stage = "cutting the main road shapes";
                            fitting.Add(RoadSolidCutter.CutPolygon(document, mainShapeIds.Select(document.GetElement).OfType<DirectShape>().ToList(), polygon));
                        }

                        if (request.SideJoin is { } join)
                        {
                            progress.Report("Joining the side road to the junction...");
                            stage = "trimming the side road shapes";
                            fitting.Add(RoadSolidCutter.JoinSide(document, sideShapeIds.Select(document.GetElement).OfType<DirectShape>().ToList(), join));
                        }

                        if (request.SurfaceOutline is { } outline)
                        {
                            progress.Report("Creating the road surface...");
                            stage = "creating the road surface";
                            TJunctionSurfaceBuilder.DeletePrevious(document, request.Key);
                            var category = RoadBuilder.GetCategories(document).First().Category;
                            surfaceMade = TJunctionSurfaceBuilder.Create(document, outline, request.SurfaceThickness, category, $"T-Junction {request.SideName}", request.Key) is not null;
                        }

                        transaction.Commit();
                    }
                }
            }

            group.Assimilate();

            var text = $"Alignment '{request.SideName}' joined to the main road and {request.KerbReturns.Count} kerb return alignments created"
                + (request.SurfaceOutline is null ? "." : surfaceMade ? ", with the road surface." : ". The road surface could not be created.");
            if (fitting.VoidCuts > 0)
            {
                text += $"{Environment.NewLine}{fitting.VoidCuts} road part families now hold a junction void. Use Remove T-Junction to put the road back; deleting a void in the Family Editor does not restore a part that was cut directly.";
            }

            if (fitting.Changed + fitting.Removed + fitting.Extended > 0)
            {
                text += $"{Environment.NewLine}Road shapes: {fitting.Changed} cut or trimmed, {fitting.Removed} removed, {fitting.Extended} extended to the junction.";
            }

            if ((request.CutPolygon is not null || request.SideJoin is not null)
                && fitting.VoidCuts + fitting.Changed + fitting.Removed + fitting.Extended == 0)
            {
                text += $"{Environment.NewLine}No existing road geometry was found to fit (roads must have been made by TopoAlign).";
            }

            if (fitting.Warnings.Count > 0)
            {
                text += $"{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, fitting.Warnings.Distinct())}";
            }

            TaskDialog.Show("T-Junction", text);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "T-junction failed");
            if (group.GetStatus() == TransactionStatus.Started)
            {
                group.RollBack();
            }

            TaskDialog.Show("T-Junction", $"The T-junction failed while {stage}: {ex.Message}");
            return Result.Failed;
        }
    }
}
