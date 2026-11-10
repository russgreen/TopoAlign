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
        try
        {
            using (var progress = ProgressService.Start("T-Junction", cancelEnabled: false))
            {
                progress.Report("Trimming the side road...");
                AlignmentApi.Update(App.RevitDocument, request.Side, request.SideJson, request.SidePlanLength);

                foreach (var kerb in request.KerbReturns)
                {
                    progress.Report($"Creating '{kerb.Name}'...");
                    AlignmentApi.Create(App.RevitDocument, kerb.Name, kerb.Json, kerb.PlanLength);
                }
            }

            var surfaceMade = false;
            if (request.SurfaceOutline is { } outline)
            {
                using var surfaceProgress = ProgressService.Start("T-Junction", cancelEnabled: false);
                surfaceProgress.Report("Creating the road surface...");
                var category = RoadBuilder.GetCategories(App.RevitDocument).First().Category;
                using var transaction = new Transaction(App.RevitDocument, "Create junction surface");
                transaction.Start();
                surfaceMade = TJunctionSurfaceBuilder.Create(App.RevitDocument, outline, request.SurfaceThickness, category, $"T-Junction {request.SideName}") is not null;
                if (surfaceMade)
                {
                    transaction.Commit();
                }
                else
                {
                    transaction.RollBack();
                }
            }

            TaskDialog.Show("T-Junction", $"Alignment '{request.SideName}' joined to the main road and {request.KerbReturns.Count} kerb return alignments created"
                + (request.SurfaceOutline is null ? "." : surfaceMade ? ", with the road surface." : ". The road surface could not be created."));
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "T-junction failed");
            TaskDialog.Show("T-Junction", ex.Message);
            return Result.Failed;
        }
    }
}
