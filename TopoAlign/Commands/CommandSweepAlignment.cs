using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandSweepAlignment : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandSweepAlignment>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandSweepAlignment));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!AlignmentApi.Supports(AlignmentApi.AlignmentFeature.Read, out var problem))
        {
            TaskDialog.Show("Sweep Along Alignment", problem);
            return Result.Cancelled;
        }

        var viewModel = new SweepAlignmentViewModel();

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

        var view = new SweepAlignmentView(viewModel);

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

        // Build after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            var result = RoadBuilder.Create(App.RevitDocument, request.Alignment, request.Profile, request.Options);

            var summary = $"Road created along '{request.AlignmentName}' from {result.StationCount} sections.";
            if (result.ReplacedPrevious)
            {
                summary += " The earlier road for this alignment was replaced.";
            }

            if (!string.IsNullOrEmpty(result.Notice))
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.Notice}";
            }

            if (result.SkippedSpans > 0)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.SkippedSpans} span{(result.SkippedSpans == 1 ? string.Empty : "s")} could not be built, " +
                           "so the road has gaps there. This usually means a curve is tighter than the road is wide: try a narrower section.";
            }

            TaskDialog.Show("Sweep Along Alignment", summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Sweep along alignment failed");
            TaskDialog.Show("Sweep Along Alignment", ex.Message);
            return Result.Failed;
        }
    }
}
