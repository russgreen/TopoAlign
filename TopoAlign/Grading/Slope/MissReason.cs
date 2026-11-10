using TopoAlign.Alignments;

namespace TopoAlign.Grading.Slope;

/// <summary>Why a point along the line got no slope.</summary>
internal enum MissReason
{
    None,

    /// <summary>The point on the line isn't over the toposolid.</summary>
    LineOffSurface,

    /// <summary>The slope did not meet the ground within the maximum width.</summary>
    NoGroundWithinWidth,

    /// <summary>The slope leaves the toposolid straight away, so there is no width to grade.</summary>
    LeavesAtOnce
}
