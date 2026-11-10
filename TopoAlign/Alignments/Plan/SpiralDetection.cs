namespace TopoAlign.Alignments.Plan;

internal sealed class SpiralDetection
{
    public SpiralDetection(int tangentOffset, double length)
    {
        TangentOffset = tangentOffset;
        Length = length;
    }

    /// <summary>How many segments away from the arc the straight is that the spiral grows out of (1 when there is no spiral).</summary>
    public int TangentOffset { get; }

    public double Length { get; }

    public bool Exists => TangentOffset > 1;
}
