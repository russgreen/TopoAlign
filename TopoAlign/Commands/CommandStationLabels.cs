using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.Civil;
using TopoAlign.ViewModels.AlignmentTools;
using TopoAlign.Views.AlignmentTools;
using TopoAlign.Commands.Shared;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandStationLabels : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandStationLabels>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandStationLabels));

        CommandFlow.Initialise(commandData);

        if (!CommandFlow.AlignmentFeaturesSupported(AlignmentFeature.Read | AlignmentFeature.StationLabels, "Station Labels"))
        {
            return Result.Cancelled;
        }

        var viewModel = new StationLabelsViewModel();

        // an alignment that is already selected is used without asking
        CommandFlow.UseSelectedElement(commandData.Application.ActiveUIDocument, new AlignmentPickFilter(), viewModel.SelectAlignment);

        var view = new StationLabelsView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
        // then the same dialog is shown again.
        var dialogResult = CommandFlow.ShowDialogWithPicks(view, () => viewModel.PickRequested = false, () => viewModel.PickRequested, viewModel.PickAlignmentElement);

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
