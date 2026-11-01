namespace TopoAlign.Alignments;

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

internal sealed class PiResult
{
    public PiResult(List<PlanSegment> segments, List<string> errors)
    {
        Segments = segments;
        Errors = errors;
    }

    public List<PlanSegment> Segments { get; }

    /// <summary>User-readable reasons the PIs can't be laid out; empty when the result is valid.</summary>
    public List<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Lays out an alignment from PIs in the usual road way: tangent lines between PIs, rounded at each interior PI with a
/// circular arc of the given radius, optionally with a clothoid transition spiral on each side.
/// </summary>
internal static class PiGeometry
{
    private const double Epsilon = 1e-9;
    private const double StraightAngle = 1e-7;

    private sealed class Corner
    {
        public bool HasCurve;
        public double Turn;            // signed deflection angle, left positive
        public double Radius;
        public double SpiralLength;
        public double SpiralAngle;
        public double TangentLength;
    }

    public static PiResult Create(IReadOnlyList<PiPoint> pis)
    {
        var errors = new List<string>();
        var segments = new List<PlanSegment>();

        if (pis is null || pis.Count < 2)
        {
            errors.Add("An alignment needs at least two points.");
            return new PiResult(segments, errors);
        }

        // directions of the tangent lines between PIs
        var count = pis.Count;
        var headings = new double[count - 1];
        var lengths = new double[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            var dx = pis[i + 1].X - pis[i].X;
            var dy = pis[i + 1].Y - pis[i].Y;
            lengths[i] = Math.Sqrt(dx * dx + dy * dy);
            if (lengths[i] < 1e-6)
            {
                errors.Add($"Points {i + 1} and {i + 2} are in the same place.");
            }

            headings[i] = Math.Atan2(dy, dx);
        }

        if (errors.Count > 0)
        {
            return new PiResult(segments, errors);
        }

        // size the curve at every interior PI
        var corners = new Corner[count];
        for (var i = 1; i < count - 1; i++)
        {
            corners[i] = SizeCorner(i, pis[i], headings[i - 1], headings[i], errors);
        }

        if (errors.Count > 0)
        {
            return new PiResult(segments, errors);
        }

        // neighbouring curves must fit between their PIs
        for (var i = 0; i < count - 1; i++)
        {
            var before = i >= 1 && corners[i].HasCurve ? corners[i].TangentLength : 0.0;
            var after = i + 1 <= count - 2 && corners[i + 1].HasCurve ? corners[i + 1].TangentLength : 0.0;
            if (before + after > lengths[i] + 1e-6)
            {
                errors.Add($"The curves at points {i + 1} and {i + 2} overlap: their tangent lengths ({before:F2} + {after:F2} ft) " +
                           $"are longer than the {lengths[i]:F2} ft between the points. Use a smaller radius or spiral length.");
            }
        }

        if (errors.Count > 0)
        {
            return new PiResult(segments, errors);
        }

        // build the segments
        var cursor = new PlanPoint(pis[0].X, pis[0].Y);
        for (var i = 1; i < count; i++)
        {
            var isLast = i == count - 1;
            if (isLast || !corners[i].HasCurve)
            {
                var target = new PlanPoint(pis[i].X, pis[i].Y);
                AddLine(segments, cursor, target);
                cursor = target;
                continue;
            }

            var corner = corners[i];
            var sign = corner.Turn > 0.0 ? 1.0 : -1.0;
            var pi = new PlanPoint(pis[i].X, pis[i].Y);
            var inHeading = headings[i - 1];
            var outHeading = headings[i];

            var tangentStart = new PlanPoint(pi.X - corner.TangentLength * Math.Cos(inHeading), pi.Y - corner.TangentLength * Math.Sin(inHeading));
            AddLine(segments, cursor, tangentStart);

            var arcStart = tangentStart;
            var arcStartHeading = inHeading;
            if (corner.SpiralLength > Epsilon)
            {
                var entry = new PlanSpiral(tangentStart, inHeading, 0.0, sign / corner.Radius, corner.SpiralLength);
                segments.Add(entry);
                arcStart = entry.EndPoint;
                arcStartHeading = inHeading + sign * corner.SpiralAngle;
            }

            var arcSweep = Math.Abs(corner.Turn) - 2.0 * corner.SpiralAngle;
            var normalAngle = arcStartHeading + sign * Math.PI / 2.0;
            var center = new PlanPoint(arcStart.X + corner.Radius * Math.Cos(normalAngle), arcStart.Y + corner.Radius * Math.Sin(normalAngle));
            var startAngle = Math.Atan2(arcStart.Y - center.Y, arcStart.X - center.X);
            var arc = new PlanArc(center, corner.Radius, startAngle, sign * arcSweep);
            segments.Add(arc);

            cursor = arc.EndPoint;
            if (corner.SpiralLength > Epsilon)
            {
                var exitHeading = arcStartHeading + sign * arcSweep;
                var exit = new PlanSpiral(arc.EndPoint, exitHeading, sign / corner.Radius, 0.0, corner.SpiralLength);
                segments.Add(exit);
                cursor = exit.EndPoint;
            }

            // the curve must end on the outgoing tangent, 'TangentLength' beyond the PI
            var expected = new PlanPoint(pi.X + corner.TangentLength * Math.Cos(outHeading), pi.Y + corner.TangentLength * Math.Sin(outHeading));
            if (PlanPoint.Distance(cursor, expected) > 1e-3)
            {
                errors.Add($"Point {i + 1}: the curve does not close onto the next line (off by {PlanPoint.Distance(cursor, expected):F4} ft).");
            }

            cursor = expected;
        }

        return new PiResult(segments, errors);
    }

    private static Corner SizeCorner(int index, PiPoint pi, double inHeading, double outHeading, List<string> errors)
    {
        var corner = new Corner();
        var turn = NormalizeAngle(outHeading - inHeading);
        corner.Turn = turn;

        if (Math.Abs(turn) < StraightAngle || pi.Radius <= Epsilon)
        {
            // a straight-through point or an explicit sharp corner
            corner.HasCurve = false;
            return corner;
        }

        if (Math.Abs(turn) > Math.PI - 1e-3)
        {
            errors.Add($"Point {index + 1}: the alignment doubles back on itself.");
            return corner;
        }

        corner.HasCurve = true;
        corner.Radius = pi.Radius;
        corner.SpiralLength = Math.Max(pi.SpiralLength, 0.0);
        corner.SpiralAngle = corner.SpiralLength / (2.0 * pi.Radius);

        var arcAngle = Math.Abs(turn) - 2.0 * corner.SpiralAngle;
        if (arcAngle <= 1e-6)
        {
            errors.Add($"Point {index + 1}: the spirals ({corner.SpiralLength:F2} ft each) are too long for a {Math.Abs(turn) * 180.0 / Math.PI:F1} degree turn " +
                       $"at radius {pi.Radius:F2} ft. Use shorter spirals or a smaller radius.");
            return corner;
        }

        double shift = 0.0, offset = 0.0;
        if (corner.SpiralLength > Epsilon)
        {
            var spiral = new PlanSpiral(new PlanPoint(0.0, 0.0), 0.0, 0.0, 1.0 / pi.Radius, corner.SpiralLength);
            var end = spiral.EndPoint;
            shift = end.Y - pi.Radius * (1.0 - Math.Cos(corner.SpiralAngle));
            offset = end.X - pi.Radius * Math.Sin(corner.SpiralAngle);
        }

        corner.TangentLength = (pi.Radius + shift) * Math.Tan(Math.Abs(turn) / 2.0) + offset;
        return corner;
    }

    private static void AddLine(List<PlanSegment> segments, PlanPoint from, PlanPoint to)
    {
        if (PlanPoint.Distance(from, to) > 1e-6)
        {
            segments.Add(new PlanLine(from, to));
        }
    }

    internal static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2.0 * Math.PI;
        }

        while (angle <= -Math.PI)
        {
            angle += 2.0 * Math.PI;
        }

        return angle;
    }

    /// <summary>
    /// Reads a picked chain of lines and arcs back as PIs (a line, a tangent arc and a line become one PI with the arc's radius),
    /// so transition spirals can be added with <see cref="Create"/>. Returns null, with a reason, when the chain is not that shape
    /// (it starts or ends with an arc, has two arcs in a row, or an arc is not tangent to its neighbours).
    /// </summary>
    public static List<PiPoint> FromSegments(IReadOnlyList<PlanSegment> segments, double spiralLength, out string reason)
    {
        reason = null;
        if (segments is null || segments.Count == 0)
        {
            reason = "There is no geometry to add spirals to.";
            return null;
        }

        if (segments.Any(s => s is PlanSpiral))
        {
            reason = "The chain already contains spirals.";
            return null;
        }

        if (segments[0] is not PlanLine || segments[segments.Count - 1] is not PlanLine)
        {
            reason = "Spirals can only be added when the chain starts and ends with a straight.";
            return null;
        }

        var pis = new List<PiPoint> { new PiPoint(segments[0].StartPoint.X, segments[0].StartPoint.Y, 0.0, 0.0) };

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment is PlanArc arc)
            {
                if (segments[i - 1] is not PlanLine before || segments[i + 1] is not PlanLine after)
                {
                    reason = "Spirals can only be added to an arc that sits between two straights.";
                    return null;
                }

                if (Math.Abs(NormalizeAngle(before.HeadingAt(0.0) - arc.HeadingAt(0.0))) > 0.02 ||
                    Math.Abs(NormalizeAngle(after.HeadingAt(0.0) - arc.HeadingAt(arc.Length))) > 0.02)
                {
                    reason = "An arc is not tangent to the straights either side of it.";
                    return null;
                }

                if (!TryIntersect(before, after, out var intersection))
                {
                    reason = "The straights either side of an arc do not meet.";
                    return null;
                }

                pis.Add(new PiPoint(intersection.X, intersection.Y, arc.Radius, spiralLength));
            }
            else if (i > 0 && segments[i - 1] is PlanLine previousLine && segment is PlanLine line &&
                     Math.Abs(NormalizeAngle(line.HeadingAt(0.0) - previousLine.HeadingAt(0.0))) > StraightAngle)
            {
                pis.Add(new PiPoint(line.StartPoint.X, line.StartPoint.Y, 0.0, 0.0));
            }
        }

        var last = segments[segments.Count - 1].EndPoint;
        pis.Add(new PiPoint(last.X, last.Y, 0.0, 0.0));
        return pis;
    }

    private static bool TryIntersect(PlanLine a, PlanLine b, out PlanPoint point)
    {
        var d1x = a.EndPoint.X - a.StartPoint.X;
        var d1y = a.EndPoint.Y - a.StartPoint.Y;
        var d2x = b.EndPoint.X - b.StartPoint.X;
        var d2y = b.EndPoint.Y - b.StartPoint.Y;
        var cross = d1x * d2y - d1y * d2x;
        if (Math.Abs(cross) < 1e-12)
        {
            point = default;
            return false;
        }

        var t = ((b.StartPoint.X - a.StartPoint.X) * d2y - (b.StartPoint.Y - a.StartPoint.Y) * d2x) / cross;
        point = new PlanPoint(a.StartPoint.X + d1x * t, a.StartPoint.Y + d1y * t);
        return true;
    }
}
