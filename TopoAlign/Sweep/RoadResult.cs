using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

internal sealed class RoadResult
{
    /// <summary>The DirectShape, or the family instance when the road was made as a family.</summary>
    public ElementId RoadId { get; set; } = ElementId.InvalidElementId;

    /// <summary>The family, when the road was made as one.</summary>
    public ElementId FamilyId { get; set; } = ElementId.InvalidElementId;

    /// <summary>Something the user should know, for example that the smooth method fell back to segments.</summary>
    public string Notice { get; set; } = string.Empty;

    public int StationCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; the road has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when an earlier road for the same alignment was removed first.</summary>
    public bool ReplacedPrevious { get; set; }

    /// <summary>The toposolid, when the road was only used to make one. No road is added to the model then.</summary>
    public ElementId ToposolidId { get; set; } = ElementId.InvalidElementId;

    /// <summary>How many surface points the toposolid was made from.</summary>
    public int ToposolidPointCount { get; set; }

    /// <summary>True when Revit would not make the toposolid with its points in one step, so they were added afterwards.</summary>
    public bool ToposolidPointsAddedAfterwards { get; set; }
}
