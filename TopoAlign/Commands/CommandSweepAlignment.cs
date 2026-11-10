using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.Civil;
using TopoAlign.Sweep.Assemblies;
using TopoAlign.Sweep.Road;
using TopoAlign.ViewModels.RoadTools;
using TopoAlign.Views.RoadTools;

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

        if (!AlignmentApi.Supports(AlignmentFeature.Read, out var problem))
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
            viewModel.PickToposolidRequested = false;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested)
            {
                if (viewModel.PickToposolidRequested)
                {
                    viewModel.PickToposolidElement();
                }
                else
                {
                    viewModel.PickAlignmentElement();
                }
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
            if (request.Assembly is not null)
            {
                return ShowAssemblyResult(request, AssemblyBuilder.Create(App.RevitDocument, request.Alignment, request.Assembly, request.AssemblyProfiles, request.Options, request.Topographies));
            }

            var result = RoadBuilder.Create(App.RevitDocument, request.Alignment, request.Profile!, request.Options);

            var summary = request.Options.Output == RoadOutput.Toposolid
                ? $"Toposolid created along '{request.AlignmentName}' from {result.StationCount} sections and {result.ToposolidPointCount} surface points. No road was added to the model."
                : $"Road created along '{request.AlignmentName}' from {result.StationCount} sections.";
            if (result.ToposolidPointsAddedAfterwards)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}Revit would not make the toposolid with its points in one step, " +
                           "so the boundary was made first and the points added to it afterwards.";
            }

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

    /// <summary>What grading the ground did on each side, or why it was not done; empty when no grading was asked for.</summary>
    internal static string GradingSummary(AssemblyResult result)
    {
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(result.GradingNotice))
        {
            lines.Add(result.GradingNotice);
        }

        foreach (var side in result.Grading)
        {
            var label = string.IsNullOrEmpty(side.ToposolidName) ? side.SideName : $"{side.SideName} ({side.ToposolidName})";
            if (!side.Succeeded)
            {
                lines.Add($"{label}: the ground was not graded. {side.Error}");
                continue;
            }

            var line = $"{label}: graded to the ground with {side.Cut} cut and {side.Fill} fill points";
            if (side.Missed > 0)
            {
                line += $", {side.Missed} point{(side.Missed == 1 ? string.Empty : "s")} where the slope did not meet the ground";
                var reasons = new List<string>();
                if (side.MissedOffSurface > 0)
                {
                    reasons.Add($"{side.MissedOffSurface} where the road's edge is not on the toposolid");
                }

                if (side.MissedTooWide > 0)
                {
                    reasons.Add($"{side.MissedTooWide} where it was further than the maximum width");
                }

                if (reasons.Count > 0)
                {
                    line += $" ({string.Join("; ", reasons)})";
                }
            }

            if (side.Clipped > 0)
            {
                line += $", {side.Clipped} where it ran off the edge of the toposolid";
            }

            lines.Add(line + ".");

            if (!string.IsNullOrEmpty(side.CornerFailure))
            {
                lines.Add($"  Revit said, about the corners: {side.CornerFailure}");
            }

            if (side.FallbackLevel > 0)
            {
                lines.Add("  Revit would not build the shape from all the points, so a simpler edit was made:" +
                          (side.PointsWereThinnedOut ? " only well-spaced points clear of the toposolid's edge were used;" : string.Empty) +
                          (side.EdgePointsWentInside ? " points for the edge went 0.3 ft inside it;" : string.Empty) +
                          (side.CornerHeightsLeftAlone ? " the corner heights were not set;" : string.Empty));
                lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd(';') + ".";
                foreach (var rejection in side.Rejections)
                {
                    lines.Add($"    Revit said {rejection}");
                }
            }
            else if (side.EdgeStepsIn > 0)
            {
                lines.Add($"  {side.EdgeStepsIn} point{(side.EdgeStepsIn == 1 ? string.Empty : "s")} on the toposolid's edge went just inside it, as Revit would not take them on the edge.");
            }

            if (side.EdgePointsRefused > 0)
            {
                lines.Add($"  {side.EdgePointsRefused} point{(side.EdgePointsRefused == 1 ? string.Empty : "s")} on the toposolid's edge could not be added, so the edge there keeps its old height.");
            }
            else if (side.CornersNotMoved > 0)
            {
                lines.Add($"  {side.CornersNotMoved} corner point{(side.CornersNotMoved == 1 ? string.Empty : "s")} on the toposolid's edge could not be moved, so points were put beside {(side.CornersNotMoved == 1 ? "it" : "them")} at the graded height instead.");
            }
        }

        return lines.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static Result ShowAssemblyResult(SweepAlignmentRequest request, AssemblyResult result)
    {
        var partWord = result.BuiltParts.Count == 1 ? "part" : "parts";
        var summary = $"Road assembly '{request.Assembly!.Name}' created along '{request.AlignmentName}': " +
                      $"{result.BuiltParts.Count} {partWord} from {result.StationCount} sections ({string.Join(", ", result.BuiltParts)}).";

        if (result.ReplacedPrevious)
        {
            summary += " The earlier road for this alignment was replaced.";
        }

        if (result.SkippedParts.Count > 0)
        {
            summary += $"{Environment.NewLine}{Environment.NewLine}These parts were not built:{Environment.NewLine}" +
                       string.Join(Environment.NewLine, result.SkippedParts);
        }

        summary += GradingSummary(result);

        if (!string.IsNullOrEmpty(result.Notice))
        {
            summary += $"{Environment.NewLine}{Environment.NewLine}{result.Notice}";
        }

        if (result.SkippedSpans > 0)
        {
            summary += $"{Environment.NewLine}{Environment.NewLine}{result.SkippedSpans} span{(result.SkippedSpans == 1 ? string.Empty : "s")} could not be built, " +
                       "so some parts have gaps there. This usually means a curve is tighter than a part is wide or far from the alignment: try a narrower part.";
        }

        TaskDialog.Show("Sweep Along Alignment", summary);
        return Result.Succeeded;
    }
}
