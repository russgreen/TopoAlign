namespace TopoAlign.Alignments.Plan;

/// <summary>The result of laying out a T-junction. <see cref="Errors"/> is empty when the layout is valid.</summary>
internal sealed class TJunctionResult
{
    public TJunctionResult(IReadOnlyList<PlanSegment> trimmedSide, PlanArc? kerbLeft, PlanArc? kerbRight, PlanPoint junction, IReadOnlyList<string> errors)
    {
        TrimmedSide = trimmedSide;
        KerbLeft = kerbLeft;
        KerbRight = kerbRight;
        Junction = junction;
        Errors = errors;
    }

    /// <summary>The side road, in its original direction, ending on the main road's centreline.</summary>
    public IReadOnlyList<PlanSegment> TrimmedSide { get; }

    /// <summary>Kerb return on the left of the side road when travelling towards the main road. It runs from the side road to the main road.</summary>
    public PlanArc? KerbLeft { get; }

    /// <summary>Kerb return on the right of the side road when travelling towards the main road.</summary>
    public PlanArc? KerbRight { get; }

    /// <summary>Where the side road centreline meets the main road centreline.</summary>
    public PlanPoint Junction { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Pure plan geometry (feet, any consistent coordinate system) for a simple T-junction: the side road is trimmed or extended to the
/// main road's centreline and each side gets a kerb return, a circular arc of the kerb radius tangent to the side road's kerb line
/// and the main road's kerb line.
/// </summary>
internal static class TJunctionGeometry
{
    private const double Eps = 1e-9;

    /// <param name="main">The main road plan. The side road must meet one of its straights.</param>
    /// <param name="side">The side road plan. Its end that meets the main road is used; see <paramref name="useOtherEnd"/>.</param>
    /// <param name="kerbRadius">Radius of the kerb returns.</param>
    /// <param name="sideHalfWidth">Distance from the side road's centreline to its kerb.</param>
    /// <param name="mainHalfWidth">Distance from the main road's centreline to its kerb.</param>
    /// <param name="useOtherEnd">Use the end of the side road that is not the nearest to the main road.</param>
    public static TJunctionResult Create(IReadOnlyList<PlanSegment> main, IReadOnlyList<PlanSegment> side, double kerbRadius,
        double sideHalfWidth, double mainHalfWidth, bool useOtherEnd = false)
    {
        var errors = new List<string>();

        if (main is null || main.Count == 0 || side is null || side.Count == 0)
        {
            return Fail(side, "Both alignments need a plan.");
        }

        if (!(kerbRadius > 0.0))
        {
            return Fail(side, "The kerb radius must be greater than zero.");
        }

        if (sideHalfWidth < 0.0 || mainHalfWidth < 0.0)
        {
            return Fail(side, "Half-widths can't be negative.");
        }

        // find the nearest end of the side road that reaches the main road
        var atEnd = TryHit(main, side, out var endHit, out var endMessage);
        var atStart = TryHit(main, PlanSegment.ReverseChain(side), out var startHit, out var startMessage);

        bool useStart;
        if (atEnd && atStart)
        {
            useStart = endHit.Distance > startHit.Distance;
            if (useOtherEnd)
            {
                useStart = !useStart;
            }
        }
        else if (atEnd || atStart)
        {
            useStart = atStart;
            if (useOtherEnd)
            {
                return Fail(side, "Only one end of the side road reaches the main road.");
            }
        }
        else
        {
            return Fail(side, endMessage ?? startMessage ?? "The side road does not meet a straight of the main road.");
        }

        var oriented = useStart ? PlanSegment.ReverseChain(side) : side.ToList();
        var hit = useStart ? startHit : endHit;
        var junction = hit.Point;

        var last = (PlanLine)oriented[oriented.Count - 1];
        var length = last.Length;
        var d = Unit(last.EndPoint.X - last.StartPoint.X, last.EndPoint.Y - last.StartPoint.Y);
        var mainLine = (PlanLine)main[hit.MainIndex];
        var m = Unit(mainLine.EndPoint.X - mainLine.StartPoint.X, mainLine.EndPoint.Y - mainLine.StartPoint.Y);

        // trimmed or extended side road, ending on the main centreline
        var trimmed = new List<PlanSegment>(oriented.Count);
        for (var i = 0; i < oriented.Count - 1; i++)
        {
            trimmed.Add(oriented[i]);
        }

        trimmed.Add(new PlanLine(last.StartPoint, junction));
        var trimmedSide = useStart ? PlanSegment.ReverseChain(trimmed) : trimmed;

        // the main road side the side road comes from, as a unit normal of the main road pointing towards it
        var toSide = new PlanPoint(last.StartPoint.X - junction.X, last.StartPoint.Y - junction.Y);
        var mainNormal = new PlanPoint(-m.Y, m.X);
        if (mainNormal.X * toSide.X + mainNormal.Y * toSide.Y < 0.0)
        {
            mainNormal = new PlanPoint(-mainNormal.X, -mainNormal.Y);
        }

        var sideNormal = new PlanPoint(-d.Y, d.X); // left of travel towards the main road
        var left = Fillet(junction, d, m, sideNormal, +1, mainNormal, kerbRadius, sideHalfWidth, mainHalfWidth, last, mainLine, "left", errors);
        var right = Fillet(junction, d, m, sideNormal, -1, mainNormal, kerbRadius, sideHalfWidth, mainHalfWidth, last, mainLine, "right", errors);

        return new TJunctionResult(trimmedSide, left, right, junction, errors);
    }

    /// <summary>
    /// The plan distance from the start of the chain to the nearest point on one of its straights. Used to read a profile at a
    /// point beside the alignment; arcs only add their length.
    /// </summary>
    public static double StationAlong(IReadOnlyList<PlanSegment> chain, PlanPoint point)
    {
        var best = double.MaxValue;
        var station = 0.0;
        var start = 0.0;
        foreach (var segment in chain)
        {
            if (segment is PlanLine line)
            {
                var dx = line.EndPoint.X - line.StartPoint.X;
                var dy = line.EndPoint.Y - line.StartPoint.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (length > Eps)
                {
                    var t = Math.Max(0.0, Math.Min(length, ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / length));
                    var nearest = new PlanPoint(line.StartPoint.X + dx / length * t, line.StartPoint.Y + dy / length * t);
                    var distance = PlanPoint.Distance(nearest, point);
                    if (distance < best)
                    {
                        best = distance;
                        station = start + t;
                    }
                }
            }

            start += segment.Length;
        }

        return station;
    }

    /// <summary>Applies a point mapping (for example a coordinate transform) to lines and arcs. The mapping must be a rigid motion.</summary>
    public static List<PlanSegment> Map(IReadOnlyList<PlanSegment> segments, Func<PlanPoint, PlanPoint> map)
    {
        var result = new List<PlanSegment>(segments.Count);
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case PlanLine line:
                    result.Add(new PlanLine(map(line.StartPoint), map(line.EndPoint)));
                    break;

                case PlanArc arc:
                    var center = map(arc.Center);
                    var start = map(arc.StartPoint);
                    result.Add(new PlanArc(center, arc.Radius, Math.Atan2(start.Y - center.Y, start.X - center.X), arc.Sweep));
                    break;

                default:
                    throw new NotSupportedException($"{segment.GetType().Name} can't be mapped.");
            }
        }

        return result;
    }

    private readonly struct Hit
    {
        public Hit(PlanPoint point, int mainIndex, double distance)
        {
            Point = point;
            MainIndex = mainIndex;
            Distance = distance;
        }

        public PlanPoint Point { get; }
        public int MainIndex { get; }

        /// <summary>How far the side road's end is from the junction.</summary>
        public double Distance { get; }
    }

    /// <summary>
    /// Looks for the main-road straight that the last straight of <paramref name="side"/> reaches when run forward (extended if it
    /// stops short). Takes the nearest.
    /// </summary>
    private static bool TryHit(IReadOnlyList<PlanSegment> main, IReadOnlyList<PlanSegment> side, out Hit hit, out string? message)
    {
        hit = default;
        message = null;

        if (side[side.Count - 1] is not PlanLine last)
        {
            message = "The side road must finish with a straight where it meets the main road.";
            return false;
        }

        var dx = last.EndPoint.X - last.StartPoint.X;
        var dy = last.EndPoint.Y - last.StartPoint.Y;
        var found = false;
        var bestT = double.MaxValue;

        for (var i = 0; i < main.Count; i++)
        {
            if (main[i] is not PlanLine line)
            {
                continue;
            }

            var ex = line.EndPoint.X - line.StartPoint.X;
            var ey = line.EndPoint.Y - line.StartPoint.Y;
            var cross = dx * ey - dy * ex;
            if (Math.Abs(cross) < 1e-12 * Math.Max(1.0, (Math.Abs(dx) + Math.Abs(dy)) * (Math.Abs(ex) + Math.Abs(ey))))
            {
                continue;
            }

            var qx = line.StartPoint.X - last.StartPoint.X;
            var qy = line.StartPoint.Y - last.StartPoint.Y;
            var t = (qx * ey - qy * ex) / cross; // along the side road, 0 at its start of the last straight, 1 at its end
            var s = (qx * dy - qy * dx) / cross; // along the main straight, 0..1
            if (t <= Eps || s < -Eps || s > 1.0 + Eps || t >= bestT)
            {
                continue;
            }

            bestT = t;
            found = true;
            var point = new PlanPoint(last.StartPoint.X + dx * t, last.StartPoint.Y + dy * t);
            hit = new Hit(point, i, Math.Abs(t - 1.0) * last.Length);
        }

        if (!found)
        {
            message = "The side road does not meet a straight of the main road.";
        }

        return found;
    }

    private static PlanArc? Fillet(PlanPoint junction, PlanPoint d, PlanPoint m, PlanPoint sideNormal, int sign, PlanPoint mainNormal,
        double radius, double sideHalf, double mainHalf, PlanLine sideLast, PlanLine mainLine, string name, List<string> errors)
    {
        // the main road direction that runs away from the side road on this side
        var along = m.X * sideNormal.X * sign + m.Y * sideNormal.Y * sign;
        if (Math.Abs(along) < 1e-9)
        {
            errors.Add("The side road runs parallel to the main road.");
            return null;
        }

        var u2 = along > 0.0 ? m : new PlanPoint(-m.X, -m.Y);
        var u1 = new PlanPoint(-d.X, -d.Y); // back along the side road, away from the junction

        // corner of the two kerb lines
        var sideKerb = new PlanPoint(junction.X + sideNormal.X * sign * sideHalf, junction.Y + sideNormal.Y * sign * sideHalf);
        var mainKerb = new PlanPoint(junction.X + mainNormal.X * mainHalf, junction.Y + mainNormal.Y * mainHalf);
        var sideKerbLine = new PlanLine(sideKerb, new PlanPoint(sideKerb.X + d.X, sideKerb.Y + d.Y));
        var mainKerbLine = new PlanLine(mainKerb, new PlanPoint(mainKerb.X + m.X, mainKerb.Y + m.Y));
        if (!PiGeometry.TryIntersect(sideKerbLine, mainKerbLine, out var corner))
        {
            errors.Add("The side road runs parallel to the main road.");
            return null;
        }

        var cosTheta = Math.Max(-1.0, Math.Min(1.0, u1.X * u2.X + u1.Y * u2.Y));
        var theta = Math.Acos(cosTheta);
        if (theta < 1e-3 || theta > Math.PI - 1e-3)
        {
            errors.Add($"The roads meet at too shallow an angle for the {name} kerb return.");
            return null;
        }

        var tangent = radius / Math.Tan(theta / 2.0);
        var t1 = new PlanPoint(corner.X + u1.X * tangent, corner.Y + u1.Y * tangent);
        var t2 = new PlanPoint(corner.X + u2.X * tangent, corner.Y + u2.Y * tangent);

        // the kerb return must start on the side road's last straight and finish on the main road straight
        // (the side road is extended or trimmed to the junction, so measure against that)
        var sideLength = PlanPoint.Distance(sideLast.StartPoint, junction);
        var p1 = Project(t1, sideLast.StartPoint, d);
        if (p1 < -Eps || p1 > sideLength + Eps)
        {
            errors.Add($"The {name} kerb return is longer than the side road. Use a smaller radius.");
            return null;
        }

        var mainDir = Unit(mainLine.EndPoint.X - mainLine.StartPoint.X, mainLine.EndPoint.Y - mainLine.StartPoint.Y);
        var p2 = Project(t2, mainLine.StartPoint, mainDir);
        if (p2 < -Eps || p2 > mainLine.Length + Eps)
        {
            errors.Add($"The {name} kerb return runs past the end of the main road straight. Use a smaller radius.");
            return null;
        }

        var bisector = Unit(u1.X + u2.X, u1.Y + u2.Y);
        var centerDistance = radius / Math.Sin(theta / 2.0);
        var center = new PlanPoint(corner.X + bisector.X * centerDistance, corner.Y + bisector.Y * centerDistance);

        var a1 = Math.Atan2(t1.Y - center.Y, t1.X - center.X);
        var a2 = Math.Atan2(t2.Y - center.Y, t2.X - center.X);
        var sweep = PiGeometry.NormalizeAngle(a2 - a1);
        return new PlanArc(center, radius, a1, sweep);
    }

    private static double Project(PlanPoint p, PlanPoint origin, PlanPoint direction) =>
        (p.X - origin.X) * direction.X + (p.Y - origin.Y) * direction.Y;

    private static PlanPoint Unit(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        return new PlanPoint(x / length, y / length);
    }

    private static TJunctionResult Fail(IReadOnlyList<PlanSegment>? side, string error) =>
        new(side ?? Array.Empty<PlanSegment>(), null, null, default, new[] { error });
}
