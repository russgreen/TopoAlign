using TopoAlign.Grading.TopoEdge;

namespace TopoAlign.Grading.Kerb;

/// <summary>
/// An ordered run of a topography's outline, with heights: the kerb line a ramp is cut into, or the edge whose level it drops to.
/// Positions along it are plan distances (arc length) in feet. A closed outline wraps round.
/// </summary>
internal sealed class KerbPath
{
    private const double Snap = 1e-4;
    private const double Epsilon = 1e-9;

    private readonly GradePoint[] _points;
    private readonly double[] _cumulative;

    private KerbPath(List<GradePoint> points, bool closed)
    {
        _points = points.ToArray();
        IsClosed = closed;

        _cumulative = new double[_points.Length];
        for (var i = 1; i < _points.Length; i++)
        {
            _cumulative[i] = _cumulative[i - 1] + PlanMath.Distance(_points[i - 1], _points[i]);
        }
    }

    /// <summary>True when the outline comes back to where it started. The first point is then repeated as the last.</summary>
    public bool IsClosed { get; }

    public double Length => _cumulative[_cumulative.Length - 1];

    public IReadOnlyList<GradePoint> Points => _points;

    private int SegmentCount => _points.Length - 1;

    /// <summary>
    /// The run of outline segments that joins end to end through the segment nearest (x, y). It stops where the outline
    /// branches or runs out, or when it comes back to its start.
    /// </summary>
    public static KerbPath FromSegments(IReadOnlyList<EdgeSegment> segments, double x, double y)
    {
        if (segments is null || segments.Count == 0)
        {
            throw new ArgumentException("There is no outline to follow.", nameof(segments));
        }

        var best = -1;
        var bestDistance = double.MaxValue;
        var at = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (PlanMath.Distance(segment.Start, segment.End) < Epsilon)
            {
                continue;
            }

            var distance = DistanceToSegment(x, y, segment.Start, segment.End, out _);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }

            foreach (var key in new[] { KeyOf(segment.Start), KeyOf(segment.End) })
            {
                if (!at.TryGetValue(key, out var list))
                {
                    at[key] = list = new List<int>();
                }

                list.Add(i);
            }
        }

        if (best < 0)
        {
            throw new ArgumentException("The outline has no length in plan.", nameof(segments));
        }

        var used = new HashSet<int> { best };

        // adds segments onto the end of the list until the outline branches, runs out, or returns to the list's first point
        bool Extend(List<GradePoint> chain)
        {
            var startKey = KeyOf(chain[0]);
            while (true)
            {
                var node = chain[chain.Count - 1];
                if (!at.TryGetValue(KeyOf(node), out var touching) || touching.Count != 2)
                {
                    return false;
                }

                var next = touching.Where(index => !used.Contains(index)).DefaultIfEmpty(-1).First();
                if (next < 0)
                {
                    return false;
                }

                used.Add(next);
                var segment = segments[next];
                var far = KeyOf(segment.Start) == KeyOf(node) ? segment.End : segment.Start;
                chain.Add(far);

                if (KeyOf(far) == startKey)
                {
                    return true;
                }
            }
        }

        var first = segments[best];
        var forward = new List<GradePoint> { first.Start, first.End };
        var closed = Extend(forward);

        if (!closed)
        {
            var backward = new List<GradePoint> { first.Start };
            Extend(backward);
            backward.Reverse();
            backward.RemoveAt(backward.Count - 1);
            forward.InsertRange(0, backward);
        }

        return new KerbPath(forward, closed);
    }

    /// <summary>Where along the path the point nearest (x, y) is, and how far (x, y) is from it.</summary>
    public void Project(double x, double y, out double position, out double distance)
    {
        position = 0.0;
        distance = double.MaxValue;
        for (var i = 0; i < SegmentCount; i++)
        {
            var d = DistanceToSegment(x, y, _points[i], _points[i + 1], out var t);
            if (d < distance)
            {
                distance = d;
                position = _cumulative[i] + t * (_cumulative[i + 1] - _cumulative[i]);
            }
        }
    }

    /// <summary>The point at the given position, with its height interpolated. A closed path wraps; an open one stops at its ends.</summary>
    public GradePoint PointAt(double position)
    {
        position = Normalize(position);
        var i = SegmentIndex(position, forward: true);
        var length = _cumulative[i + 1] - _cumulative[i];
        var t = length < Epsilon ? 0.0 : (position - _cumulative[i]) / length;
        var a = _points[i];
        var b = _points[i + 1];
        return new GradePoint(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
    }

    /// <summary>Unit plan direction of travel at the position. At a corner, the segment after it or before it.</summary>
    public (double X, double Y) DirectionAt(double position, bool forward = true)
    {
        position = Normalize(position);
        var i = SegmentIndex(position, forward);
        var a = _points[i];
        var b = _points[i + 1];
        var length = PlanMath.Distance(a, b);
        return ((b.X - a.X) / length, (b.Y - a.Y) / length);
    }

    /// <summary>How far the path turns, in radians, at the given position. Zero along a straight piece.</summary>
    public double TurnAt(double position)
    {
        var before = DirectionAt(position, forward: false);
        var after = DirectionAt(position, forward: true);
        var dot = before.X * after.X + before.Y * after.Y;
        return Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot)));
    }

    /// <summary>
    /// Positions of the path's own corner points strictly between the two positions, in order. Positions may run outside
    /// 0..length on a closed path, which then wraps.
    /// </summary>
    public List<double> VertexPositions(double from, double to)
    {
        var found = new List<double>();
        var count = IsClosed ? _points.Length - 1 : _points.Length;
        var wraps = IsClosed ? new[] { -1, 0, 1, 2 }.Select(k => k + (int)Math.Floor(from / Length)).ToArray() : new[] { 0 };

        foreach (var k in wraps)
        {
            for (var i = 0; i < count; i++)
            {
                var u = _cumulative[i] + k * Length;
                if (u > from + Epsilon && u < to - Epsilon)
                {
                    found.Add(u);
                }
            }
        }

        found.Sort();
        return found;
    }

    /// <summary>
    /// Points along the path from one position to the other: both ends, every corner in between, and extra points so none
    /// are further apart than the spacing.
    /// </summary>
    public List<GradePoint> Sample(double from, double to, double spacing)
    {
        if (!(spacing > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), "The spacing must be greater than zero.");
        }

        var keys = new List<double> { from };
        keys.AddRange(VertexPositions(from, to));
        keys.Add(to);

        var positions = new List<double>();
        for (var k = 0; k < keys.Count - 1; k++)
        {
            var length = keys[k + 1] - keys[k];
            var pieces = Math.Max(1, (int)Math.Ceiling(length / spacing - 1e-9));
            for (var j = 0; j < pieces; j++)
            {
                positions.Add(keys[k] + j * length / pieces);
            }
        }

        positions.Add(to);
        return positions.Select(PointAt).ToList();
    }

    /// <summary>How far can be walked from the position in the direction (+1 or -1) before the path ends. Half way round on a closed path.</summary>
    public double DistanceAvailable(double position, int direction)
    {
        if (IsClosed)
        {
            return Length / 2.0;
        }

        return Math.Max(0.0, direction > 0 ? Length - position : position);
    }

    private double Normalize(double position)
    {
        if (IsClosed)
        {
            position %= Length;
            return position < 0.0 ? position + Length : position;
        }

        return Math.Max(0.0, Math.Min(Length, position));
    }

    private int SegmentIndex(double position, bool forward)
    {
        var last = SegmentCount - 1;
        if (forward)
        {
            for (var i = last; i > 0; i--)
            {
                if (_cumulative[i] <= position + Epsilon)
                {
                    return i;
                }
            }

            return 0;
        }

        if (IsClosed && position < Epsilon)
        {
            return last;
        }

        for (var i = 0; i < last; i++)
        {
            if (_cumulative[i + 1] >= position - Epsilon)
            {
                return i;
            }
        }

        return last;
    }

    private static (long, long) KeyOf(GradePoint point) => ((long)Math.Round(point.X / Snap), (long)Math.Round(point.Y / Snap));

    private static double DistanceToSegment(double px, double py, GradePoint a, GradePoint b, out double t)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        t = lengthSquared < 1e-18 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((px - a.X) * dx + (py - a.Y) * dy) / lengthSquared));
        var cx = a.X + t * dx;
        var cy = a.Y + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }
}
