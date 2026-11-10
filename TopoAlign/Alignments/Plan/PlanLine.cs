namespace TopoAlign.Alignments.Plan;

internal sealed class PlanLine : PlanSegment
{
    private readonly PlanPoint _start;
    private readonly PlanPoint _end;
    private readonly double _length;
    private readonly double _heading;

    public PlanLine(PlanPoint start, PlanPoint end)
    {
        _start = start;
        _end = end;
        _length = PlanPoint.Distance(start, end);
        _heading = Math.Atan2(end.Y - start.Y, end.X - start.X);
    }

    public override double Length => _length;
    public override PlanPoint StartPoint => _start;
    public override PlanPoint EndPoint => _end;

    public override PlanPoint PointAt(double distance)
    {
        if (_length <= 0.0)
        {
            return _start;
        }

        var t = Math.Max(0.0, Math.Min(1.0, distance / _length));
        return new PlanPoint(_start.X + (_end.X - _start.X) * t, _start.Y + (_end.Y - _start.Y) * t);
    }

    public override double HeadingAt(double distance) => _heading;

    public override PlanSegment Reversed() => new PlanLine(_end, _start);
}
