#if REVIT2024_OR_GREATER
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Grading;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandGradeToSurface : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandGradeToSurface>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandGradeToSurface));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        //// the side of the line is picked with a point, which needs a plan to click in
        //if (App.RevitDocument.ActiveView is not ViewPlan)
        //{
        //    TaskDialog.Show("Grade to Surface", "Please make sure you are in a plan view before running this command.");
        //    return Result.Failed;
        //}

        var viewModel = new GradeToSurfaceViewModel();

        // a toposolid that is already selected is used without asking
        var uidoc = commandData.Application.ActiveUIDocument;
        var filter = new TopoPickFilter();
        foreach (var id in uidoc.Selection.GetElementIds())
        {
            var element = App.RevitDocument.GetElement(id);
            if (filter.AllowElement(element) && viewModel.SelectToposolid(element))
            {
                break;
            }
        }

        var view = new GradeToSurfaceView(viewModel);

        // The dialog hides itself to let the user pick; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        bool pickRequested;
        do
        {
            viewModel.PickRequested = GradePickKind.None;
            dialogResult = view.ShowDialog();

            pickRequested = viewModel.PickRequested != GradePickKind.None;
            if (pickRequested)
            {
                viewModel.RunPick();
            }
        }
        while (pickRequested);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Edit after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            var result = ToposolidGrader.Apply(App.RevitDocument, request);

            var summary = $"Graded {result.Cut + result.Fill} of {result.Stations} points along the line " +
                          $"({result.Fill} fill, {result.Cut} cut).{Environment.NewLine}" +
                          $"Removed {result.PointsRemoved} existing toposolid points and added {result.PointsAdded}.";
            if (result.FallbackLevel > 0)
            {
                summary += $"{Environment.NewLine}Revit would not build the shape from all the points, so a simpler edit was made:" +
                           (result.PointsWereThinnedOut ? " only well-spaced points clear of the toposolid's edge were used;" : string.Empty) +
                           (result.EdgePointsWentInside ? " points for the edge went 0.3 ft inside it;" : string.Empty) +
                           (result.CornerHeightsLeftAlone ? " the corner heights were not set;" : string.Empty);
                summary = summary.TrimEnd(';') + ".";
                foreach (var rejection in result.Rejections)
                {
                    summary += $"{Environment.NewLine}  Revit said {rejection}";
                }
            }

            if (result.Clipped > 0)
            {
                summary += $"{Environment.NewLine}{result.Clipped} point(s) ran to the edge of the toposolid before the slope met the ground, so the slope ends on the edge there.";
            }

            if (result.PointsThinned > 0)
            {
                summary += $"{Environment.NewLine}{result.PointsThinned} point(s) were left out because they were too close to another point, for example on the inside of a tight bend.";
            }

            if (result.PointsInset > 0)
            {
                summary += $"{Environment.NewLine}{result.PointsInset} point(s) were placed 15 mm inside the toposolid's edge, because Revit would not add points on the edge itself.";
            }

            if (result.CornersMoved > 0)
            {
                summary += $"{Environment.NewLine}{result.CornersMoved} corner point(s) on the toposolid's edge were set to the graded height.";
            }

            if (result.PointsOutside > 0)
            {
                var furthest = UnitUtils.ConvertFromInternalUnits(result.FurthestOutside, UnitTypeId.Millimeters);
                summary += $"{Environment.NewLine}{result.PointsOutside} point(s) were left out because they were outside the toposolid (furthest {furthest:0.#} mm).";
            }

            if (result.PointsRefused > 0)
            {
                summary += $"{Environment.NewLine}{result.PointsRefused} point(s) were left out because Revit would not add them.";
            }

            if (result.MovesFailed > 0)
            {
                summary += $"{Environment.NewLine}{result.MovesFailed} corner point(s) on the toposolid's edge could not be set to the graded height.";
                if (!string.IsNullOrWhiteSpace(result.MoveFailure))
                {
                    summary += $"{Environment.NewLine}  Reason: {result.MoveFailure}";
                }
            }

            if (result.Missed > 0)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.Missed} points along the line were left out:";
                if (result.MissedTooWide > 0)
                {
                    summary += $"{Environment.NewLine}  {result.MissedTooWide} - the slope did not meet the ground within the maximum width.";
                }

                if (result.MissedOffSurface > 0)
                {
                    summary += $"{Environment.NewLine}  {result.MissedOffSurface} - the point is not over the toposolid.";
                }

                if (result.MissedAtEdge > 0)
                {
                    summary += $"{Environment.NewLine}  {result.MissedAtEdge} - the point is on the toposolid's edge and the slope runs off it.";
                }
            }

            TaskDialog.Show("Grade to Surface", summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Grade to surface failed");
            TaskDialog.Show("Grade to Surface", ex.Message);
            return Result.Failed;
        }
    }
}
#endif
