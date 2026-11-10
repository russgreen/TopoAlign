using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep.Assemblies;

internal sealed class AssemblyResult
{
    /// <summary>The DirectShapes made, one per part that produced geometry.</summary>
    public List<ElementId> ShapeIds { get; } = new();

    /// <summary>The names of the parts that were built.</summary>
    public List<string> BuiltParts { get; } = new();

    /// <summary>Parts that could not be built, with the reason.</summary>
    public List<string> SkippedParts { get; } = new();

    /// <summary>Something the user should know, for example a part that fell back from smooth surfaces.</summary>
    public string Notice { get; set; } = string.Empty;

    public int StationCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; a part has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when an earlier road for the same alignment was removed first.</summary>
    public bool ReplacedPrevious { get; set; }

    /// <summary>What grading the topo out from each side did; empty when no topo was given or no side has grading turned on.</summary>
    public List<SideGradeOutcome> Grading { get; } = new();

    /// <summary>Why the topo was not graded at all, for example the element is not a toposolid.</summary>
    public string GradingNotice { get; set; } = string.Empty;
}
