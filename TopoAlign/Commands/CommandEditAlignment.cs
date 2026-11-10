using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.Civil;
using TopoAlign.Views.AlignmentTools;
using TopoAlign.ViewModels.AlignmentTools;
using TopoAlign.Commands.Shared;
using TopoAlign.Services;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandEditAlignment : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandEditAlignment>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandEditAlignment));

        CommandFlow.Initialise(commandData);

        if (!CommandFlow.AlignmentFeaturesSupported(AlignmentFeature.Read | AlignmentFeature.Update, "Edit Alignment"))
        {
            return Result.Cancelled;
        }

        var viewModel = new EditAlignmentViewModel();

        // an alignment that is already selected is used without asking
        CommandFlow.UseSelectedElement(commandData.Application.ActiveUIDocument, new AlignmentPickFilter(), viewModel.SelectAlignment);

        var view = new EditAlignmentView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
        // then the same dialog is shown again.
        var dialogResult = CommandFlow.ShowDialogWithPicks(view, () => viewModel.PickRequested = false, () => viewModel.PickRequested, viewModel.RunPendingPick);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Update after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            using (var progress = ProgressService.Start("Edit alignment", cancelEnabled: false))
            {
                progress.Report("Updating the alignment in Revit...");
                AlignmentApi.Update(App.RevitDocument, request.Alignment, request.Json, request.PlanLength, request.Renamed ? request.Name : null);
            }

            var changes = new List<string>();
            if (request.Renamed)
            {
                changes.Add($"renamed to '{request.Name}'");
            }

            if (request.StartStationChanged)
            {
                changes.Add("start station changed");
            }

            if (request.PlanChanged)
            {
                changes.Add("points, radii or spirals edited");
            }

            if (request.Reversed)
            {
                changes.Add("direction reversed");
            }

            TaskDialog.Show("Edit Alignment", $"Alignment '{request.Name}' updated: {string.Join(", ", changes)}.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Edit alignment failed");
            TaskDialog.Show("Edit Alignment", ex.Message);
            return Result.Failed;
        }
    }
}
