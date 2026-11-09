using System.Text.Json.Serialization;
using TopoAlign.Grading;

namespace TopoAlign.Sweep;

/// <summary>
/// A named set of parts that are swept along an alignment together, for example a carriageway, a kerb either side and
/// a footway beyond each kerb. Pure data with no Revit types; it is saved as JSON by <see cref="AssemblyStore"/>.
/// </summary>
internal sealed class SweepAssembly
{
    public string Name { get; set; } = string.Empty;

    public List<AssemblyPart> Parts { get; set; } = new();

    /// <summary>How the ground is graded out from the left of the assembly (to the left of the direction of travel).</summary>
    public SideGrading Left { get; set; } = new();

    /// <summary>How the ground is graded out from the right of the assembly.</summary>
    public SideGrading Right { get; set; } = new();
}
