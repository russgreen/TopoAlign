using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.Civil;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandEditAlignment : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandEditAlignment>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandEditAlignment));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!AlignmentApi.Supports(AlignmentFeature.Read | AlignmentFeature.Update, out var problem))
        {
            TaskDialog.Show("Edit Alignment", problem);
            return Result.Cancelled;
        }

        var viewModel = new EditAlignmentViewModel();

        // an alignment that is already selected is used without asking
        var uidoc = commandData.Application.ActiveUIDocument;
        var filter = new AlignmentPickFilter();
        foreach (var id in uidoc.Selection.GetElementIds())
        {
            var element = App.RevitDocument.GetElement(id);
            if (filter.AllowElement(element) && viewModel.SelectAlignment(element))
            {
                break;
            }
        }

        var view = new EditAlignmentView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
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

        // Update after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            AlignmentApi.Update(App.RevitDocument, request.Alignment, request.Json, request.PlanLength, request.Renamed ? request.Name : null);

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
