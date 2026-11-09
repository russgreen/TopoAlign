using Autodesk.Revit.DB;
using Serilog;

namespace TopoAlign.Sweep;

internal sealed class KerbResult
{
    /// <summary>The DirectShape, or the family instance when the kerb was made as a family.</summary>
    public ElementId KerbId { get; set; } = ElementId.InvalidElementId;

    /// <summary>The family, when the kerb was made as one.</summary>
    public ElementId FamilyId { get; set; } = ElementId.InvalidElementId;

    public int RunCount { get; set; }
    public int SectionCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; the kerb has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when the profile was flipped to the other side of the edges during review.</summary>
    public bool Flipped { get; set; }

    /// <summary>True when the user cancelled at review; nothing was added to the model.</summary>
    public bool Cancelled { get; set; }

    /// <summary>Something the user should know, for example a run that was left out.</summary>
    public string Notice { get; set; } = string.Empty;
}
