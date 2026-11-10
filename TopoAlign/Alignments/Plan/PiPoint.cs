namespace TopoAlign.Alignments.Plan;

/// <summary>A point of intersection (PI) of an alignment, with the curve to put in the corner.</summary>
internal readonly struct PiPoint
{
    public PiPoint(double x, double y, double radius, double spiralLength)
    {
        X = x;
        Y = y;
        Radius = radius;
        SpiralLength = spiralLength;
    }

    public double X { get; }
    public double Y { get; }

    /// <summary>Radius of the curve at this PI in feet; 0 leaves a sharp corner. Ignored at the first and last PI.</summary>
    public double Radius { get; }

    /// <summary>Length of each transition spiral in feet (entry and exit); 0 for a simple arc.</summary>
    public double SpiralLength { get; }
}
