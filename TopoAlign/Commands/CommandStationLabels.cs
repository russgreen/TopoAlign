using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandStationLabels : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandStationLabels>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandStationLabels));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!AlignmentApi.Supports(AlignmentApi.AlignmentFeature.Read | AlignmentApi.AlignmentFeature.StationLabels, out var problem))
        {
            TaskDialog.Show("Station Labels", problem);
            return Result.Cancelled;
        }

        var viewModel = new StationLabelsViewModel();

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

        var view = new StationLabelsView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        do
        {
            viewModel.PickRequested = false;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested)
            {
                viewModel.PickAlignmentElement();
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
            var count = AlignmentApi.CreateStationLabels(
                App.RevitDocument,
                request.Alignment,
                request.View,
                request.LabelTypeId,
                request.StartStation,
                request.EndStation,
                request.Interval,
                request.Offset,
                request.HasLeader);

            TaskDialog.Show("Station Labels", $"{count} station label{(count == 1 ? string.Empty : "s")} created for '{request.AlignmentName}' in '{request.View.Name}'.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Create station labels failed");
            TaskDialog.Show("Station Labels", ex.Message);
            return Result.Failed;
        }
    }
}
