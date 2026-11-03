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

        // the side of the line is picked with a point, which needs a plan to click in
        if (App.RevitDocument.ActiveView is not ViewPlan)
        {
            TaskDialog.Show("Grade to Surface", "Please make sure you are in a plan view before running this command.");
            return Result.Failed;
        }

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
            if (result.Missed > 0)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.Missed} points were left out because their slope did not meet the toposolid within the maximum width.";
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
