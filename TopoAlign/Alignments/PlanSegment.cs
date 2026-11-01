namespace TopoAlign.Alignments;

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

/// <summary>
/// One piece of a horizontal alignment in plan: a straight, a circular arc or a clothoid spiral.
/// Pure geometry with no Revit types, so alignments can be laid out and tested without a model.
/// </summary>
internal abstract class PlanSegment
{
    public abstract double Length { get; }
    public abstract PlanPoint StartPoint { get; }
    public abstract PlanPoint EndPoint { get; }

    /// <summary>Point at a distance along the segment, 0 to <see cref="Length"/>.</summary>
    public abstract PlanPoint PointAt(double distance);

    /// <summary>Direction of travel, in radians from +X, at a distance along the segment.</summary>
    public abstract double HeadingAt(double distance);

    /// <summary>The same segment traversed the other way.</summary>
    public abstract PlanSegment Reversed();

    /// <summary>Reverses a whole chain: the order of the segments and the direction of each.</summary>
    public static List<PlanSegment> ReverseChain(IReadOnlyList<PlanSegment> segments)
    {
        var result = new List<PlanSegment>(segments.Count);
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            result.Add(segments[i].Reversed());
        }

        return result;
    }

    /// <summary>Total length of a chain.</summary>
    public static double TotalLength(IEnumerable<PlanSegment> segments)
    {
        var total = 0.0;
        foreach (var segment in segments)
        {
            total += segment.Length;
        }

        return total;
    }
}

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

/// <summary>
/// A clothoid (Euler spiral): curvature changes linearly with distance from <c>startCurvature</c> to
/// <c>endCurvature</c> (1/radius, positive for a left turn). A transition into a curve starts at 0.
/// </summary>
internal sealed class PlanSpiral : PlanSegment
{
    private const double IntegrationStep = 0.25;

    private readonly PlanPoint _start;
    private readonly double _startHeading;
    private readonly double _startCurvature;
    private readonly double _endCurvature;
    private readonly double _length;

    public PlanSpiral(PlanPoint start, double startHeading, double startCurvature, double endCurvature, double length)
    {
        if (length <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A spiral needs a length greater than zero.");
        }

        _start = start;
        _startHeading = startHeading;
        _startCurvature = startCurvature;
        _endCurvature = endCurvature;
        _length = length;
    }

    public double StartCurvature => _startCurvature;
    public double EndCurvature => _endCurvature;
    public double StartHeading => _startHeading;

    public override double Length => _length;
    public override PlanPoint StartPoint => _start;
    public override PlanPoint EndPoint => PointAt(_length);

    public override double HeadingAt(double distance)
    {
        var d = Math.Max(0.0, Math.Min(_length, distance));
        return _startHeading + _startCurvature * d + (_endCurvature - _startCurvature) * d * d / (2.0 * _length);
    }

    /// <summary>Integrates the heading numerically (Simpson's rule); the step is far below the tolerance of the layout.</summary>
    public override PlanPoint PointAt(double distance)
    {
        var d = Math.Max(0.0, Math.Min(_length, distance));
        if (d <= 0.0)
        {
            return _start;
        }

        var steps = Math.Max(8, (int)Math.Ceiling(d / IntegrationStep));
        if (steps % 2 == 1)
        {
            steps++;
        }

        var h = d / steps;
        double sumX = 0.0, sumY = 0.0;
        for (var i = 0; i <= steps; i++)
        {
            var weight = i == 0 || i == steps ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
            var heading = HeadingAt(i * h);
            sumX += weight * Math.Cos(heading);
            sumY += weight * Math.Sin(heading);
        }

        return new PlanPoint(_start.X + sumX * h / 3.0, _start.Y + sumY * h / 3.0);
    }

    public override PlanSegment Reversed()
    {
        // travelling the other way flips the heading by 180 degrees and the sign of the curvature
        return new PlanSpiral(EndPoint, HeadingAt(_length) + Math.PI, -_endCurvature, -_startCurvature, _length);
    }

    /// <summary>
    /// Points along the spiral so that the chord between consecutive points deviates from the curve by no more
    /// than <paramref name="maxDeviation"/> (feet). Includes both ends.
    /// </summary>
    public List<PlanPoint> Tessellate(double maxDeviation)
    {
        var tolerance = Math.Max(maxDeviation, 1e-6);
        var maxCurvature = Math.Max(Math.Abs(_startCurvature), Math.Abs(_endCurvature));
        var pieceLength = maxCurvature <= 1e-12 ? _length : Math.Sqrt(8.0 * tolerance / maxCurvature);
        var pieces = (int)Math.Ceiling(_length / Math.Max(pieceLength, 1e-3));
        pieces = Math.Max(4, Math.Min(pieces, 500));

        var points = new List<PlanPoint>(pieces + 1);
        for (var i = 0; i <= pieces; i++)
        {
            points.Add(i == 0 ? _start : PointAt(_length * i / pieces));
        }

        return points;
    }
}
