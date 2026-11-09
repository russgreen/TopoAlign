namespace TopoAlign.Sweep;

/// <summary>A point of a road's surface, in feet.</summary>
internal readonly struct SurfaceVertex
{
    public SurfaceVertex(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}
