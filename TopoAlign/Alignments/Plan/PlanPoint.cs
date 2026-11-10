namespace TopoAlign.Alignments.Plan;

/// <summary>A point in plan (feet in the coordinate system of the segment that owns it).</summary>
internal readonly struct PlanPoint
{
    public PlanPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }

    public static double Distance(PlanPoint a, PlanPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
