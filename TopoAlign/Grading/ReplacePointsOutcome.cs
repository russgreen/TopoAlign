#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>What <see cref="ToposolidGrader.ReplacePoints"/> could not do.</summary>
internal sealed class ReplacePointsOutcome
{
    /// <summary>Points from the "try one at a time" list that Revit took.</summary>
    public int TrialAdded { get; set; }

    /// <summary>Of those, how many had to go just inside the toposolid's edge because Revit would not take a point on the edge itself.</summary>
    public int Inset { get; set; }

    /// <summary>Points Revit refused.</summary>
    public int Refused { get; set; }

    /// <summary>Vertex moves that could not be made.</summary>
    public int MovesFailed { get; set; }

    /// <summary>Why the moves failed, when they did.</summary>
    public string MoveFailure { get; set; } = string.Empty;
}
#endif
