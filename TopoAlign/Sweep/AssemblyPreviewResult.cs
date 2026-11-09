using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>What the assembly editor draws: the outline of every part whose profile is known, and the space they cover.</summary>
internal sealed class AssemblyPreviewResult
{
    public List<PreviewOutline> Outlines { get; } = new();

    /// <summary>The cut and fill slopes of each side that has grading turned on and a shoulder to start from.</summary>
    public List<PreviewSlope> Slopes { get; } = new();

    /// <summary>Parts that could not be drawn because their profile is not chosen or could not be read.</summary>
    public List<string> Missing { get; } = new();

    public bool HasOutlines => Outlines.Count > 0;

    public double MinU { get; set; }
    public double MaxU { get; set; }
    public double MinV { get; set; }
    public double MaxV { get; set; }
}
