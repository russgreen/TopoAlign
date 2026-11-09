using System.Text.Json.Serialization;
using TopoAlign.Grading;

namespace TopoAlign.Sweep;

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
