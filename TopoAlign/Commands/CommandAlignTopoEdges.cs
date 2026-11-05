using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using TopoAlign.Grading;

namespace TopoAlign.Commands;

/// <summary>Adds points to two topographies that share an edge, so that the edges line up.</summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandAlignTopoEdges : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandAlignTopoEdges>();

    private const string Title = "Align Topo Edges";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandAlignTopoEdges));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        var uidoc = commandData.Application.ActiveUIDocument;
        var document = uidoc.Document;
        var filter = new TopoPickFilter();

        Element first;
        Element second;
        try
        {
            first = document.GetElement(uidoc.Selection.PickObject(ObjectType.Element, filter, "Select the topo to add points to"));
            second = document.GetElement(uidoc.Selection.PickObject(ObjectType.Element, filter, "Select the neighbouring topo that shares an edge with it"));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }

        if (first.Id == second.Id)
        {
            TaskDialog.Show(Title, "Please pick two different topos.");
            return Result.Failed;
        }

        var dialog = new TaskDialog(Title)
        {
            MainInstruction = "Which topos should get the new points?",
            MainContent = "Points are added where the outline of one topo has a point along the edge of the other. Every existing point on the first topo's shared edge is moved to the height of the second topo's edge, including points between its vertices. The second topo's existing points are never moved.",
            CommonButtons = TaskDialogCommonButtons.Cancel           
        };

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "First topo only", "Leave the second topo as it is and add its edge points to the first.");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Both topos", "Add points to both so the shared edge matches exactly.");


        dialog.DefaultButton = TaskDialogResult.CommandLink1;

        var choice = dialog.Show();
        if (choice != TaskDialogResult.CommandLink1 && choice != TaskDialogResult.CommandLink2)
        {
            return Result.Cancelled;
        }

        try
        {
            var result = TopoEdgeAlignerRevit.Apply(document, first, second, choice == TaskDialogResult.CommandLink2);

            if (result.SharedPoints == 0 && result.AddedToFirst == 0 && result.AddedToSecond == 0 && result.MovedOnFirst == 0)
            {
                TaskDialog.Show(Title, "The two topos do not appear to share an edge, so nothing was changed.");
                return Result.Succeeded;
            }

            if (result.AddedToFirst + result.AddedToSecond + result.MovedOnFirst == 0)
            {
                TaskDialog.Show(Title, "The shared edge already has matching points, so nothing was changed.");
                return Result.Succeeded;
            }

            var summary = $"Added {result.AddedToFirst} points to the first topo";
            if (choice == TaskDialogResult.CommandLink2)
            {
                summary += $" and {result.AddedToSecond} to the second";
            }

            summary += ".";

            if (result.MovedOnFirst > 0)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}Moved {result.MovedOnFirst} existing points on the first topo to the height of the second topo's edge.";
            }

            TaskDialog.Show(Title, summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Align topo edges failed");
            TaskDialog.Show(Title, ex.Message);
            return Result.Failed;
        }
    }
}
