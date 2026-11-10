using System.Text.Json.Serialization;
using TopoAlign.Grading;
using TopoAlign.Grading.Slope;

namespace TopoAlign.Sweep.Assemblies;

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
