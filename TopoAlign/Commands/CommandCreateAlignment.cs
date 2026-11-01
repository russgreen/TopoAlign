using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandCreateAlignment : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandCreateAlignment>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandCreateAlignment));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!Alignments.AlignmentApi.IsAvailable)
        {
            TaskDialog.Show("Create Alignment", "The Autodesk Civil Alignments add-in is not available in this Revit installation.");
            return Result.Cancelled;
        }

        var viewModel = new CreateAlignmentViewModel();
        var view = new CreateAlignmentView(viewModel);

        // The dialog hides itself to let the user pick lines or a toposolid; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        do
        {
            viewModel.PickRequested = false;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested)
            {
                viewModel.RunPendingPick();
            }
        }
        while (viewModel.PickRequested);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Create after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            var createdId = Alignments.AlignmentApi.Create(
                App.RevitDocument,
                request.Name,
                request.Json,
                request.PlanLength,
                request.SourceCurveIdsToDelete);

            if (createdId is null || createdId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Create Alignment", "Alignment was not created.");
                return Result.Failed;
            }

#if REVIT2024_OR_GREATER
            var createdElementId = createdId.Value;
#else
            var createdElementId = createdId.IntegerValue;
#endif
            TaskDialog.Show("Create Alignment", $"Alignment '{request.Name}' created (Id: {createdElementId}).");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Create alignment failed");
            TaskDialog.Show("Create Alignment", ex.Message);
            return Result.Failed;
        }
    }
}
