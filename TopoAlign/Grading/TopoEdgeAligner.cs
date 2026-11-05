namespace TopoAlign.Grading;

/// <summary>One piece of a topography's outline, in feet.</summary>
internal readonly struct EdgeSegment
{
    public EdgeSegment(GradePoint start, GradePoint end)
    {
        Start = start;
        End = end;
    }

    public GradePoint Start { get; }
    public GradePoint End { get; }
}

/// <summary>The points that make two neighbouring topographies meet along the edge they share.</summary>
internal sealed class TopoEdgeAlignment
{
    /// <summary>Points to add to the first topography, so its edge passes through the second one's edge points.</summary>
    public List<GradePoint> AddToFirst { get; } = new();

    /// <summary>Points to add to the second topography, so its edge passes through the first one's edge points.</summary>
    public List<GradePoint> AddToSecond { get; } = new();

    /// <summary>
    /// Points the first topography already has, at the place and the height they should have to meet the second topography's edge.
    /// A topography can't hold two points in the same place, so these are moved rather than added. The second topography is the reference and isn't moved.
    /// </summary>
    public List<GradePoint> ChangeOnFirst { get; } = new();

    public int SharedPoints { get; set; }
}

/// <summary>
/// Works out which points to add to two topographies that touch along an edge so that the edges line up.
/// Works on plain coordinates, so it needs no Revit geometry.
/// </summary>
internal static class TopoEdgeAligner
{
    // outline vertices this close (in plan) to an edge sit on it, and two vertices this close are the same point
    public const double DefaultPlanTolerance = 0.01;

    // coincident vertices this close in height are already aligned
    public const double HeightTolerance = 0.005;

    // snaps triangle corners together when working out which triangle edges are on the outline
    private const double SnapSize = 1e-4;

    /// <summary>
    /// The outline of a mesh: the triangle edges used by exactly one triangle, which includes the edges of any holes.
    /// Each triangle is given as its three corners.
    /// </summary>
    public static List<EdgeSegment> BoundarySegments(IEnumerable<(GradePoint A, GradePoint B, GradePoint C)> triangles)
    {
        if (triangles is null)
        {
            throw new ArgumentNullException(nameof(triangles));
        }

        var edges = new Dictionary<(Key, Key), (int Count, GradePoint Start, GradePoint End)>();

        void Add(GradePoint start, GradePoint end)
        {
            var a = KeyOf(start);
            var b = KeyOf(end);
            if (a.Equals(b))
            {
                return;
            }

            // the same edge seen from the neighbouring triangle runs the other way
            var key = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
            edges[key] = edges.TryGetValue(key, out var seen) ? (seen.Count + 1, seen.Start, seen.End) : (1, start, end);
        }

        foreach (var (a, b, c) in triangles)
        {
            Add(a, b);
            Add(b, c);
            Add(c, a);
        }

        return edges.Values
            .Where(edge => edge.Count == 1)
            .Select(edge => new EdgeSegment(edge.Start, edge.End))
            .ToList();
    }

    /// <summary>
    /// Aligns every existing point on the shared boundary of the first topography to the second edge's interpolated height.
    /// Adds missing edge vertices, using the second topography as the reference for both modes.
    /// </summary>
    public static TopoEdgeAlignment Align(
        IReadOnlyList<EdgeSegment> first,
        IReadOnlyList<EdgeSegment> second,
        bool both,
        double planTolerance = DefaultPlanTolerance,
        IReadOnlyList<GradePoint>? firstPoints = null)
    {
        if (first is null)
        {
            throw new ArgumentNullException(nameof(first));
        }

        if (second is null)
        {
            throw new ArgumentNullException(nameof(second));
        }

        var result = new TopoEdgeAlignment();

        // Stored points include collinear boundary points that the mesh may omit.
        var firstVertices = firstPoints is { Count: > 0 }
            ? firstPoints.Where(point => TryProjectToEdge(point, first, planTolerance, out _)).ToList()
            : UniqueVertices(first, planTolerance);
        var seen = new List<GradePoint>();
        foreach (var vertex in firstVertices)
        {
            if (seen.Any(point => PlanDistance(point, vertex) <= planTolerance))
            {
                continue;
            }
            seen.Add(vertex);
            if (TryProjectToEdge(vertex, second, planTolerance, out var target) && Math.Abs(vertex.Z - target.Z) > HeightTolerance)
            {
                result.ChangeOnFirst.Add(new GradePoint(vertex.X, vertex.Y, target.Z));
            }
        }

        Project(UniqueVertices(second, planTolerance), first, result.AddToFirst, false, planTolerance, out var shared, firstVertices);
        result.SharedPoints = shared;

        if (both)
        {
            Project(seen, second, result.AddToSecond, true, planTolerance, out _);
        }

        return result;
    }

    private static void Project(IEnumerable<GradePoint> vertices, IReadOnlyList<EdgeSegment> onto,
        List<GradePoint> added, bool useTargetHeight, double tolerance, out int shared, IReadOnlyList<GradePoint>? existing = null)
    {
        shared = 0;
        foreach (var vertex in vertices)
        {
            if (onto.Any(segment => PlanDistance(vertex, segment.Start) <= tolerance || PlanDistance(vertex, segment.End) <= tolerance) ||
                existing?.Any(point => PlanDistance(vertex, point) <= tolerance) == true)
            {
                shared++;
            }
            else if (TryProjectToEdge(vertex, onto, tolerance, out var target))
            {
                added.Add(useTargetHeight ? new GradePoint(vertex.X, vertex.Y, target.Z) : vertex);
            }
        }
    }

    private static bool TryProjectToEdge(GradePoint point, IReadOnlyList<EdgeSegment> segments, double tolerance, out GradePoint projected)
    {
        projected = default;
        var best = tolerance;
        var found = false;
        foreach (var segment in segments)
        {
            var candidate = ClosestPointOnSegment(point, segment);
            var distance = PlanDistance(point, candidate);
            if (distance <= best)
            {
                best = distance;
                projected = candidate;
                found = true;
            }
        }
        return found;
    }

    private static List<GradePoint> UniqueVertices(IReadOnlyList<EdgeSegment> segments, double tolerance)
    {
        var vertices = new List<GradePoint>();
        var keys = new HashSet<Key>();
        foreach (var segment in segments)
        {
            foreach (var point in new[] { segment.Start, segment.End })
            {
                if (keys.Add(KeyOf(point)) && !vertices.Any(v => PlanDistance(v, point) <= tolerance && Math.Abs(v.Z - point.Z) <= HeightTolerance))
                {
                    vertices.Add(point);
                }
            }
        }

        return vertices;
    }

    private static double PlanDistance(GradePoint a, GradePoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The nearest point in plan, with height interpolated along the edge.</summary>
    private static GradePoint ClosestPointOnSegment(GradePoint point, EdgeSegment segment)
    {
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-18)
        {
            return segment.Start;
        }

        var t = ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared;
        t = Math.Max(0.0, Math.Min(1.0, t));
        var px = segment.Start.X + t * dx;
        var py = segment.Start.Y + t * dy;
        return new GradePoint(px, py, segment.Start.Z + t * (segment.End.Z - segment.Start.Z));
    }

    private static Key KeyOf(GradePoint point)
    {
        return new Key((long)Math.Round(point.X / SnapSize), (long)Math.Round(point.Y / SnapSize), (long)Math.Round(point.Z / SnapSize));
    }

    private readonly struct Key : IEquatable<Key>, IComparable<Key>
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        public Key(long x, long y, long z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public bool Equals(Key other) => _x == other._x && _y == other._y && _z == other._z;

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (int)(_x * 73856093 ^ _y * 19349663 ^ _z * 83492791);
            }
        }

        public int CompareTo(Key other)
        {
            var c = _x.CompareTo(other._x);
            if (c != 0)
            {
                return c;
            }

            c = _y.CompareTo(other._y);
            return c != 0 ? c : _z.CompareTo(other._z);
        }
    }
}
