namespace TopoAlign.Sweep;

/// <summary>
/// One cross-section of a kerb: where it stands, which way its (vertical) plane faces and how much its width is stretched
/// so that the kerb keeps its true width through a corner.
/// </summary>
internal readonly struct KerbSection
{
    public KerbSection(PathPoint centre, double dirX, double dirY, double uScale)
    {
        Centre = centre;
        DirX = dirX;
        DirY = dirY;
        UScale = uScale;
    }

    public PathPoint Centre { get; }

    /// <summary>Horizontal direction the section's plane is across (a unit vector; the section's +u is to its right).</summary>
    public double DirX { get; }

    public double DirY { get; }

    /// <summary>Factor applied to the section's u values: 1 on a straight run, 1/cos(half the turn) at a mitred corner.</summary>
    public double UScale { get; }
}

internal sealed class KerbPlanResult
{
    /// <summary>Runs of sections to loft. A run is split where the path turns too sharply to mitre.</summary>
    public List<List<KerbSection>> Runs { get; } = new();

    /// <summary>Why the path can't be swept, or null.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Turns a polyline into the sections a kerb is lofted through. Sections always stand vertical; at a corner they follow
/// the mitre (the bisector of the two directions) and are widened so the profile keeps its width, and a corner that is
/// too sharp to mitre splits the kerb in two. Pure; no Revit types.
/// </summary>
internal static class KerbPlanner
{
    /// <summary>Points closer together than this in plan (feet) are one point.</summary>
    private const double MergeDistance = 1e-4;

    /// <summary>The widest a section may be stretched at a corner; beyond it (a turn of about 130 degrees) the kerb is split instead.</summary>
    public const double DefaultMaxMitreScale = 2.5;

    private struct Vertex
    {
        public double InX, InY, OutX, OutY;
        public double MitreX, MitreY, Scale;
        public bool Split;
    }

    public static KerbPlanResult Plan(IReadOnlyList<PathPoint> points, bool closed, double maxMitreScale = DefaultMaxMitreScale)
    {
        var result = new KerbPlanResult();
        if (points is null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        var path = Clean(points, closed);
        if (path.Count < (closed ? 3 : 2))
        {
            result.Error = "The path is too short to sweep along.";
            return result;
        }

        var count = path.Count;
        var segmentCount = closed ? count : count - 1;
        var direction = new (double X, double Y)[segmentCount];
        for (var i = 0; i < segmentCount; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < MergeDistance)
            {
                result.Error = "The path has a vertical edge, which a vertical profile can't follow.";
                return result;
            }

            direction[i] = (dx / length, dy / length);
        }

        var vertices = new Vertex[count];
        for (var i = 0; i < count; i++)
        {
            vertices[i] = MakeVertex(i, count, closed, direction, maxMitreScale);
        }

        if (closed && !vertices.Any(v => v.Split))
        {
            var run = new List<KerbSection>(count + 1);
            for (var i = 0; i < count; i++)
            {
                run.Add(Mitred(path[i], vertices[i]));
            }

            run.Add(run[0]);
            result.Runs.Add(run);
            return result;
        }

        // start a closed loop at a split so it becomes an ordinary open run
        var start = closed ? Array.FindIndex(vertices, v => v.Split) : 0;
        var current = new List<KerbSection> { Section(path[start], closed ? vertices[start].OutX : direction[0].X, closed ? vertices[start].OutY : direction[0].Y) };
        for (var k = 1; k < count; k++)
        {
            var i = (start + k) % count;
            var vertex = vertices[i];
            if (vertex.Split)
            {
                current.Add(Section(path[i], vertex.InX, vertex.InY));
                result.Runs.Add(current);
                current = new List<KerbSection> { Section(path[i], vertex.OutX, vertex.OutY) };
            }
            else
            {
                current.Add(Mitred(path[i], vertex));
            }
        }

        if (closed)
        {
            current.Add(Section(path[start], vertices[start].InX, vertices[start].InY));
        }

        result.Runs.Add(current);
        return result;
    }

    private static Vertex MakeVertex(int index, int count, bool closed, (double X, double Y)[] direction, double maxMitreScale)
    {
        var vertex = new Vertex();

        var hasIn = closed || index > 0;
        var hasOut = closed || index < count - 1;
        var incoming = hasIn ? direction[(index - 1 + direction.Length) % direction.Length] : direction[index];
        var outgoing = hasOut ? direction[index % direction.Length] : direction[index - 1];

        vertex.InX = incoming.X;
        vertex.InY = incoming.Y;
        vertex.OutX = outgoing.X;
        vertex.OutY = outgoing.Y;

        if (!hasIn || !hasOut)
        {
            // the end of an open path: square to the one segment there
            vertex.MitreX = hasIn ? incoming.X : outgoing.X;
            vertex.MitreY = hasIn ? incoming.Y : outgoing.Y;
            vertex.Scale = 1.0;
            return vertex;
        }

        var sumX = incoming.X + outgoing.X;
        var sumY = incoming.Y + outgoing.Y;
        var sumLength = Math.Sqrt(sumX * sumX + sumY * sumY);
        if (sumLength < 1e-9)
        {
            vertex.Split = true;
            return vertex;
        }

        vertex.MitreX = sumX / sumLength;
        vertex.MitreY = sumY / sumLength;
        var cosine = vertex.MitreX * incoming.X + vertex.MitreY * incoming.Y;
        vertex.Scale = cosine > 1e-9 ? 1.0 / cosine : double.MaxValue;
        if (vertex.Scale > maxMitreScale)
        {
            vertex.Split = true;
        }

        return vertex;
    }

    private static KerbSection Mitred(PathPoint point, Vertex vertex) => new(point, vertex.MitreX, vertex.MitreY, vertex.Scale);

    private static KerbSection Section(PathPoint point, double dirX, double dirY) => new(point, dirX, dirY, 1.0);

    /// <summary>Drops points that repeat the one before them (and, for a loop, a last point that closes it).</summary>
    private static List<PathPoint> Clean(IReadOnlyList<PathPoint> points, bool closed)
    {
        var result = new List<PathPoint>(points.Count);
        foreach (var point in points)
        {
            if (result.Count == 0 || PathPoint.Distance(result[result.Count - 1], point) > MergeDistance)
            {
                result.Add(point);
            }
        }

        if (closed && result.Count > 1 && PathPoint.Distance(result[0], result[result.Count - 1]) <= MergeDistance)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }
}
