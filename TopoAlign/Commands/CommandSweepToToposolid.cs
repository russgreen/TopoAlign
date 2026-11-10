#if REVIT2024_OR_GREATER
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Sweep.Road;
using TopoAlign.ViewModels.RoadTools;
using TopoAlign.Views.RoadTools;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandSweepToToposolid : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandSweepToToposolid>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandSweepToToposolid));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        var viewModel = new SweepToToposolidViewModel();

        // a road that is already selected is used without asking
        var uidoc = commandData.Application.ActiveUIDocument;
        var filter = new RoadPickFilter();
        foreach (var id in uidoc.Selection.GetElementIds())
        {
            var element = App.RevitDocument.GetElement(id);
            if (filter.AllowElement(element) && viewModel.SelectRoad(element))
            {
                break;
            }
        }

        var view = new SweepToToposolidView(viewModel);

        // The dialog hides itself to let the user pick a road; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        do
        {
            viewModel.PickRequested = false;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested)
            {
                viewModel.PickRoadElement();
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
            var result = ToposolidBuilder.Create(App.RevitDocument, request.Road, request.TypeId, request.LevelId, request.DeleteRoad);

            var summary = $"Toposolid created from the road using {result.PointCount} surface points.";
            if (result.DeletedOriginal)
            {
                summary += " The original road was deleted.";
            }

            if (result.PointsAddedAfterwards)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}Revit would not make the toposolid with its points in one step, " +
                           "so the boundary was made first and the points added to it afterwards.";
            }

            if (!string.IsNullOrEmpty(result.Notice))
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.Notice}";
            }

            TaskDialog.Show("Sweep to Toposolid", summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Sweep to toposolid failed");
            TaskDialog.Show("Sweep to Toposolid", ex.Message);
            return Result.Failed;
        }
    }
}
#endif
