using TopoAlign.Alignments;

namespace TopoAlign.Sweep.Assemblies;

/// <summary>
/// A slope running out from the edge of the assembly, drawn as a dashed line: the cut slope climbs and the fill slope falls.
/// It starts at the shoulder, where the ground is graded from, and carries on outwards for as far as the picture goes.
/// </summary>
internal sealed class PreviewSlope
{
    public PreviewSlope(AssemblySide side, bool isCut, double u, double v, double riseOverRun, string label)
    {
        Side = side;
        IsCut = isCut;
        U = u;
        V = v;
        RiseOverRun = riseOverRun;
        Label = label;
    }

    public AssemblySide Side { get; }

    /// <summary>True for the cut slope (climbs), false for the fill slope (falls).</summary>
    public bool IsCut { get; }

    /// <summary>Where the slope starts, across the road and up, in feet.</summary>
    public double U { get; }

    public double V { get; }

    /// <summary>Always positive; the line goes up by this much per foot outwards for a cut and down for a fill.</summary>
    public double RiseOverRun { get; }

    /// <summary>For example "cut 1:2".</summary>
    public string Label { get; }

    /// <summary>+1 outwards to the right, -1 outwards to the left.</summary>
    public double Direction => Side == AssemblySide.Right ? 1.0 : -1.0;

    /// <summary>The height of the slope at a distance across the road.</summary>
    public double HeightAt(double u) => V + (IsCut ? 1.0 : -1.0) * RiseOverRun * (u - U) * Direction;
}
