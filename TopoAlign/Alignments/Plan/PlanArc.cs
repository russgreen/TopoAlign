namespace TopoAlign.Alignments.Plan;

internal sealed class PlanArc : PlanSegment
{
    private readonly double _length;

    /// <param name="center">Centre of the circle.</param>
    /// <param name="radius">Radius, greater than zero.</param>
    /// <param name="startAngle">Angle of the start point as seen from the centre, radians from +X.</param>
    /// <param name="sweep">Signed angle swept: positive is counter-clockwise (a left turn), negative clockwise.</param>
    public PlanArc(PlanPoint center, double radius, double startAngle, double sweep)
    {
        if (radius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "An arc needs a radius greater than zero.");
        }

        Center = center;
        Radius = radius;
        StartAngle = startAngle;
        Sweep = sweep;
        _length = radius * Math.Abs(sweep);
    }

    public PlanPoint Center { get; }
    public double Radius { get; }
    public double StartAngle { get; }
    public double Sweep { get; }

    /// <summary>True for a clockwise arc (a right turn).</summary>
    public bool IsClockwise => Sweep < 0.0;

    public override double Length => _length;
    public override PlanPoint StartPoint => AtAngle(StartAngle);
    public override PlanPoint EndPoint => AtAngle(StartAngle + Sweep);

    public override PlanPoint PointAt(double distance)
    {
        var fraction = _length <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, distance / _length));
        return AtAngle(StartAngle + Sweep * fraction);
    }

    public override double HeadingAt(double distance)
    {
        var fraction = _length <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, distance / _length));
        var angle = StartAngle + Sweep * fraction;
        return angle + (Sweep >= 0.0 ? Math.PI / 2.0 : -Math.PI / 2.0);
    }

    public override PlanSegment Reversed() => new PlanArc(Center, Radius, StartAngle + Sweep, -Sweep);

    private PlanPoint AtAngle(double angle)
    {
        return new PlanPoint(Center.X + Radius * Math.Cos(angle), Center.Y + Radius * Math.Sin(angle));
    }
}
