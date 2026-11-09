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

/// <summary>
/// The slopes that carry the assembly's outer edge down or up to the existing ground on one side: a cut slope where the ground is
/// above the edge and a fill slope where it is below. Slopes are entered as the user chooses (1:N, percent or degrees).
/// </summary>
internal sealed class SideGrading
{
    public bool Enabled { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SlopeMode SlopeMode { get; set; } = SlopeMode.Ratio;

    /// <summary>The slope where the ground is above the edge, in the units of <see cref="SlopeMode"/> (2 means 1:2 for a ratio).</summary>
    public double CutValue { get; set; } = 2.0;

    /// <summary>The slope where the ground is below the edge, in the units of <see cref="SlopeMode"/>.</summary>
    public double FillValue { get; set; } = 3.0;

    /// <summary>Furthest the slope may run, measured square to the alignment, before giving up on a point. Millimetres.</summary>
    public double MaxWidthMm { get; set; } = 30000.0;

    public double MaxWidthFeet => MaxWidthMm / AssemblyPart.MillimetresPerFoot;

    /// <summary>A slope as the user enters it, for example "1:3", "33%" or "18.4°".</summary>
    public string Describe(double value) => SlopeMode switch
    {
        SlopeMode.Percent => $"{value:0.##}%",
        SlopeMode.Degrees => $"{value:0.##}°",
        _ => $"1:{value:0.##}"
    };

    /// <summary>The cut slope as rise over run. Throws if the value isn't usable for the mode.</summary>
    public double CutRiseOverRun() => SlopeUnits.ToRiseOverRun(SlopeMode, CutValue);

    /// <summary>The fill slope as rise over run. Throws if the value isn't usable for the mode.</summary>
    public double FillRiseOverRun() => SlopeUnits.ToRiseOverRun(SlopeMode, FillValue);
}

/// <summary>
/// One profile of an assembly and where it sits relative to the alignment. Lengths are millimetres so the saved file
/// reads the same whatever units the project uses.
/// </summary>
internal sealed class AssemblyPart
{
    /// <summary>Millimetres in a foot, the unit Revit works in internally.</summary>
    public const double MillimetresPerFoot = 304.8;

    /// <summary>What the part is called, for example "Kerb (left)". Kept with the element it makes.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The profile family type, as "Family : Type" (the name <see cref="ProfileTypeInfo"/> shows).</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Across the road from the alignment, positive to the right of the direction of travel.</summary>
    public double LateralOffsetMm { get; set; }

    /// <summary>Up from the alignment's finished level.</summary>
    public double VerticalOffsetMm { get; set; }

    /// <summary>
    /// Flips the profile left to right before it is positioned. A kerb drawn for one side of the road needs this on the other side.
    /// </summary>
    public bool MirrorProfile { get; set; }

    /// <summary>The name of the <c>BuiltInCategory</c> the part is filed under, for example "OST_Roads".</summary>
    public string Category { get; set; } = string.Empty;

    public double LateralOffsetFeet => LateralOffsetMm / MillimetresPerFoot;

    public double VerticalOffsetFeet => VerticalOffsetMm / MillimetresPerFoot;

    /// <summary>The part's section as it stands in the assembly: flipped if asked, then moved to its offsets (feet).</summary>
    public SectionProfile Place(SectionProfile profile)
    {
        var placed = MirrorProfile ? profile.Mirrored() : profile;
        return placed.Offset(LateralOffsetFeet, VerticalOffsetFeet);
    }
}
