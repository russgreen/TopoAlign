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

        try
        {
            var fitting = new CutOutcome();
            var surfaceMade = false;

            using (var progress = ProgressService.Start("T-Junction", cancelEnabled: false))
            {
                progress.Report("Trimming the side road...");
                AlignmentApi.Update(document, request.Side, request.SideJson, request.SidePlanLength);

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

                // fit the roads that already exist to the junction
                if (request.CutPolygon is not null || request.SideJoin is not null || request.SurfaceOutline is not null)
                {
                    using var transaction = new Transaction(document, "Fit roads to junction");
                    transaction.Start();

                    if (request.CutPolygon is { } polygon && request.Main is { } main)
                    {
                        progress.Report("Cutting the junction out of the main road...");
                        fitting.Add(RoadSolidCutter.CutPolygon(document, RoadBuilder.FindRoads(document, main), polygon));
                    }

                    if (request.SideJoin is { } join)
                    {
                        progress.Report("Joining the side road to the junction...");
                        fitting.Add(RoadSolidCutter.JoinSide(document, RoadBuilder.FindRoads(document, request.Side), join));
                    }

                    if (request.SurfaceOutline is { } outline)
                    {
                        progress.Report("Creating the road surface...");
                        TJunctionSurfaceBuilder.DeletePrevious(document, request.Side);
                        var category = RoadBuilder.GetCategories(document).First().Category;
                        surfaceMade = TJunctionSurfaceBuilder.Create(document, outline, request.SurfaceThickness, category, $"T-Junction {request.SideName}", request.Side) is not null;
                    }

                    transaction.Commit();
                }
            }

            group.Assimilate();

            var text = $"Alignment '{request.SideName}' joined to the main road and {request.KerbReturns.Count} kerb return alignments created"
                + (request.SurfaceOutline is null ? "." : surfaceMade ? ", with the road surface." : ". The road surface could not be created.");
            if (request.CutPolygon is not null)
            {
                text += $"{Environment.NewLine}Main road: {fitting.Changed} parts cut, {fitting.Removed} removed.";
            }

            if (request.SideJoin is not null)
            {
                text += $"{Environment.NewLine}Side road: {fitting.Extended} parts extended to the junction; the rest were trimmed.";
            }

            if ((request.CutPolygon is not null || request.SideJoin is not null) && fitting.Changed + fitting.Removed + fitting.Extended == 0)
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

            TaskDialog.Show("T-Junction", ex.Message);
            return Result.Failed;
        }
    }
}
