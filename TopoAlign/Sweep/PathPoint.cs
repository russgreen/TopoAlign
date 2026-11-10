namespace TopoAlign.Sweep;

/// <summary>A point on the 3D path of an alignment, in feet.</summary>
internal readonly struct PathPoint
{
    public PathPoint(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static double Distance(PathPoint a, PathPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dz = b.Z - a.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
