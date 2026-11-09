using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Grading;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
#endif

namespace TopoAlign.Sweep;

/// <summary>What grading the ground out from one side of an assembly did.</summary>
internal sealed class SideGradeOutcome
{
    public SideGradeOutcome(AssemblySide side)
    {
        Side = side;
    }

    public AssemblySide Side { get; }

    /// <summary>The toposolid that was graded, or empty when the side met none of them.</summary>
    public string ToposolidName { get; set; } = string.Empty;

    /// <summary>Points along the edge where the ground is above the edge, so the slope climbs.</summary>
    public int Cut { get; set; }

    /// <summary>Points along the edge where the ground is below it, so the slope falls.</summary>
    public int Fill { get; set; }

    /// <summary>Points whose slope did not meet the ground.</summary>
    public int Missed { get; set; }

    /// <summary>Of the missed points, those where the road's edge is not on the toposolid at all.</summary>
    public int MissedOffSurface { get; set; }

    /// <summary>Of the missed points, those where no ground was met within the maximum width.</summary>
    public int MissedTooWide { get; set; }

    /// <summary>Points whose slope ran off the edge of the toposolid before meeting the ground.</summary>
    public int Clipped { get; set; }

    /// <summary>Corner points on the toposolid's edge that could not be set to the graded height by moving them (points were put beside them instead).</summary>
    public int CornersNotMoved { get; set; }

    /// <summary>Revit's own reason the corners could not be moved, when they could not.</summary>
    public string CornerFailure { get; set; } = string.Empty;

    /// <summary>Edge points Revit refused outright, so the edge there keeps its old height.</summary>
    public int EdgePointsRefused { get; set; }

    /// <summary>0 when the edit went in as planned; above that, Revit would not build the shape and a simpler edit was made (see <see cref="GradeToSurfaceResult.FallbackLevel"/>).</summary>
    public int FallbackLevel { get; set; }

    public bool EdgePointsWentInside { get; set; }
    public bool PointsWereThinnedOut { get; set; }
    public bool CornerHeightsLeftAlone { get; set; }

    /// <summary>What Revit said about each edit it would not build.</summary>
    public List<string> Rejections { get; } = new();

    /// <summary>Points that stand in for the toposolid's edge but were put a little way inside it, because Revit would not take them on it.</summary>
    public int EdgeStepsIn { get; set; }

    /// <summary>Why the side could not be graded, or empty if it was.</summary>
    public string Error { get; set; } = string.Empty;

    public bool Succeeded => string.IsNullOrEmpty(Error);

    public string SideName => Side == AssemblySide.Left ? "Left" : "Right";
}
