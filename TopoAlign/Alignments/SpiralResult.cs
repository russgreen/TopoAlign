namespace TopoAlign.Alignments;

internal sealed class SpiralResult
{
    public SpiralResult(List<PlanSegment> segments, int spiralledCurves, List<string> skipped)
    {
        Segments = segments;
        SpiralledCurves = spiralledCurves;
        Skipped = skipped;
    }

    public List<PlanSegment> Segments { get; }

    /// <summary>How many arcs were given entry and exit spirals.</summary>
    public int SpiralledCurves { get; }

    /// <summary>User-readable reasons an arc was left without spirals.</summary>
    public List<string> Skipped { get; }
}
