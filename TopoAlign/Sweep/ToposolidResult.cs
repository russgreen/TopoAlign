#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>What converting a road to a toposolid made.</summary>
internal sealed class ToposolidResult
{
    public ElementId ToposolidId { get; set; } = ElementId.InvalidElementId;
    public int PointCount { get; set; }
    public int LoopCount { get; set; }

    /// <summary>True when Revit would not make the toposolid from its boundary and points in one go, so the points were added afterwards.</summary>
    public bool PointsAddedAfterwards { get; set; }

    public bool DeletedOriginal { get; set; }
    public string Notice { get; set; } = string.Empty;
}
#endif
