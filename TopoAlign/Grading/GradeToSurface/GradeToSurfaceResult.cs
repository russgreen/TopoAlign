#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading.GradeToSurface;

internal sealed class GradeToSurfaceResult
{
    public int Stations { get; set; }
    public int Cut { get; set; }
    public int Fill { get; set; }
    public int Missed { get; set; }

    /// <summary>Of the missed points, how many are not over the toposolid.</summary>
    public int MissedOffSurface { get; set; }

    /// <summary>Of the missed points, how many did not meet the ground within the maximum width.</summary>
    public int MissedTooWide { get; set; }

    /// <summary>Of the missed points, how many have a slope that leaves the toposolid straight away.</summary>
    public int MissedAtEdge { get; set; }

    /// <summary>Points along the line whose slope ran off the toposolid's edge before meeting the ground, so it ends on the edge. Counted in cut/fill too.</summary>
    public int Clipped { get; set; }

    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }

    /// <summary>
    /// 0 if everything went in. Otherwise Revit rejected the shape and a simpler edit was made: 1 with only well-spaced points clear of the edge;
    /// 2 with the edge points a little inside the edge; 3 with both; 4 with those and the corner heights left alone too.
    /// </summary>
    public int FallbackLevel { get; set; }

    /// <summary>True when the points meant for the toposolid's edge went a little way inside it, because Revit would not build the shape with them on it.</summary>
    public bool EdgePointsWentInside => FallbackLevel >= 2;

    /// <summary>True when the points were thinned to well-spaced ones clear of the edge.</summary>
    public bool PointsWereThinnedOut => FallbackLevel is 1 or 3 or 4;

    /// <summary>True when the toposolid's corner heights were left alone.</summary>
    public bool CornerHeightsLeftAlone => FallbackLevel >= 4;

    /// <summary>What Revit said about each simpler edit it was asked to try, in order.</summary>
    public List<string> Rejections { get; } = new();

    /// <summary>How many points were left out by that fall-back.</summary>
    public int PointsDroppedForRetry { get; set; }

    /// <summary>New points left out because they were too close to another new point (typically on the inside of a bend).</summary>
    public int PointsThinned { get; set; }

    /// <summary>Points on the toposolid's edge that were placed just inside it, because Revit would not take them on the edge.</summary>
    public int PointsInset { get; set; }

    /// <summary>Corner points of the toposolid that were set to the graded height instead of being added.</summary>
    public int CornersMoved { get; set; }

    /// <summary>New points left out because they were outside the toposolid or Revit would not take them.</summary>
    public int PointsSkipped { get; set; }

    /// <summary>Of the skipped points, how many were outside the toposolid's outline, and the furthest of them (feet).</summary>
    public int PointsOutside { get; set; }

    public double FurthestOutside { get; set; }

    /// <summary>Of the skipped points, how many Revit refused to add.</summary>
    public int PointsRefused { get; set; }

    /// <summary>Of the skipped points, how many were corners or edge points whose height could not be set.</summary>
    public int MovesFailed { get; set; }

    /// <summary>Why the corner heights could not be set, when they could not.</summary>
    public string MoveFailure { get; set; } = string.Empty;
}
#endif
