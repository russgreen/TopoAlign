namespace TopoAlign.Alignments.Plan;

/// <summary>A point of the junction outline: plan position and the elevation of the finished surface there.</summary>
internal readonly struct SurfacePoint
{
    public SurfacePoint(PlanPoint point, double elevation)
    {
        Point = point;
        Elevation = elevation;
    }

    public PlanPoint Point { get; }
    public double Elevation { get; }
}

/// <summary>
/// Pure geometry for the road surface of a T-junction: the outline bounded by the side road's end, the two kerb returns and the
/// main road's kerb line, and its triangulation.
/// </summary>
internal static class TJunctionSurface
{
    /// <summary>The most a chord of the outline may sit inside the true arc, in feet (about 1 mm).</summary>
    public const double ArcTolerance = 0.004;

    private const int MinArcSteps = 6;
    private const int MaxArcSteps = 90;

    /// <summary>How many chords approximate an arc so that none departs from it by more than <see cref="ArcTolerance"/>.</summary>
    public static int ArcSteps(double radius, double sweep)
    {
        var step = radius > ArcTolerance
            ? 2.0 * Math.Acos(1.0 - ArcTolerance / radius)
            : Math.PI / 12.0;
        var steps = (int)Math.Ceiling(Math.Abs(sweep) / step);
        return Math.Min(MaxArcSteps, Math.Max(MinArcSteps, steps));
    }

    /// <summary>
    /// The outline, counter-clockwise seen from above: across the side road, round the left kerb return, along the main road kerb
    /// line and back round the right kerb return. Elevation is read from the side road at points on or beside the side road and from
    /// the main road at the points on the main road kerb line; along a kerb return it changes evenly from one to the other.
    /// Returns null if the layout is not valid or the outline is too small to form a surface.
    /// </summary>
    public static List<SurfacePoint>? Outline(TJunctionResult result, Func<PlanPoint, double> sideElevation, Func<PlanPoint, double> mainElevation)
    {
        if (!result.IsValid || result.KerbLeft is null || result.KerbRight is null)
        {
            return null;
        }

        var points = new List<SurfacePoint>();

        // the surface begins on the join line, square to the side road, and runs along each side kerb line to the kerb return
        // (no distance at all when the return starts on the join line)
        var join = TJunctionFootprint.Join(result);
        if (join is { } line)
        {
            points.Add(new SurfacePoint(line.Left, sideElevation(line.Left)));
        }

        // left return, from the side road kerb to the main road kerb
        AddArc(points, result.KerbLeft, sideElevation(result.KerbLeft.StartPoint), mainElevation(result.KerbLeft.EndPoint), false);

        // main road kerb line is a straight chord between the ends of the two returns; its elevation follows the main road
        // right return, from the main road kerb back to the side road kerb
        AddArc(points, result.KerbRight, sideElevation(result.KerbRight.StartPoint), mainElevation(result.KerbRight.EndPoint), true);

        if (join is { } closing)
        {
            points.Add(new SurfacePoint(closing.Right, sideElevation(closing.Right)));
        }

        // the closing edge is the join line itself, across the side road

        var cleaned = new List<SurfacePoint>();
        foreach (var p in points)
        {
            if (cleaned.Count == 0 || PlanPoint.Distance(cleaned[cleaned.Count - 1].Point, p.Point) > 1e-6)
            {
                cleaned.Add(p);
            }
        }

        while (cleaned.Count > 1 && PlanPoint.Distance(cleaned[0].Point, cleaned[cleaned.Count - 1].Point) <= 1e-6)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count < 3)
        {
            return null;
        }

        if (SignedArea(cleaned.Select(p => p.Point).ToList()) < 0.0)
        {
            cleaned.Reverse();
        }

        return cleaned;
    }

    private static void AddArc(List<SurfacePoint> points, PlanArc arc, double startElevation, double endElevation, bool reverse)
    {
        var steps = ArcSteps(arc.Radius, arc.Sweep);
        var arcPoints = new List<SurfacePoint>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var point = i == 0 ? arc.StartPoint : i == steps ? arc.EndPoint : arc.PointAt(arc.Length * t);
            arcPoints.Add(new SurfacePoint(point, startElevation + t * (endElevation - startElevation)));
        }

        if (reverse)
        {
            arcPoints.Reverse();
        }

        points.AddRange(arcPoints);
    }

    /// <summary>
    /// Splits every edge of a closed outline that is longer than <paramref name="maxEdge"/> into equal parts. The surface has edges tens
    /// of feet long (the main road kerb line, the join line) beside kerb return chords well under a foot; triangulating that gives
    /// triangles a few hundredths of a degree across, which Revit refuses to build into a solid. Evenly sized edges keep the angles sound.
    /// </summary>
    public static List<T> Densify<T>(IReadOnlyList<T> outline, double maxEdge, Func<T, T, double> distance, Func<T, T, double, T> lerp)
    {
        var result = new List<T>(outline.Count);
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            result.Add(a);
            var parts = (int)Math.Ceiling(distance(a, b) / maxEdge);
            for (var k = 1; k < parts; k++)
            {
                result.Add(lerp(a, b, (double)k / parts));
            }
        }

        return result;
    }

    /// <summary>The longest edge left on the outline after <see cref="Densify{T}"/>, in feet.</summary>
    public const double MaxOutlineEdge = 1.0;

    public static double SignedArea(IReadOnlyList<PlanPoint> polygon)
    {
        var area = 0.0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return area / 2.0;
    }

    /// <summary>
    /// Ear-clipping triangulation of a simple counter-clockwise polygon. Returns the vertex indices of each triangle (also counter-clockwise),
    /// or null if the polygon can't be triangulated.
    /// </summary>
    public static List<int[]>? Triangulate(IReadOnlyList<PlanPoint> polygon)
    {
        var n = polygon.Count;
        if (n < 3)
        {
            return null;
        }

        var remaining = Enumerable.Range(0, n).ToList();
        var triangles = new List<int[]>();
        var guard = 0;

        while (remaining.Count > 3 && guard++ < 10000)
        {
            var clipped = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                var current = remaining[i];
                var next = remaining[(i + 1) % remaining.Count];

                if (Cross(polygon[prev], polygon[current], polygon[next]) <= 1e-9)
                {
                    continue; // reflex or flat
                }

                var isEar = true;
                foreach (var other in remaining)
                {
                    if (other == prev || other == current || other == next)
                    {
                        continue;
                    }

                    if (InsideTriangle(polygon[other], polygon[prev], polygon[current], polygon[next]))
                    {
                        isEar = false;
                        break;
                    }
                }

                if (!isEar)
                {
                    continue;
                }

                triangles.Add(new[] { prev, current, next });
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                return null;
            }
        }

        if (remaining.Count == 3 && Cross(polygon[remaining[0]], polygon[remaining[1]], polygon[remaining[2]]) > 1e-9)
        {
            triangles.Add(new[] { remaining[0], remaining[1], remaining[2] });
        }
        else if (remaining.Count == 3)
        {
            return null;
        }

        Flatten(polygon, triangles);
        return triangles;
    }

    /// <summary>
    /// Flips diagonals until every one satisfies the Delaunay condition. Ear clipping fans long triangles out of a single vertex, and with
    /// the many short chords of a smooth kerb return those are slivers a fraction of a millimetre thick, which Revit refuses to build.
    /// A flip only ever swaps the diagonal of a convex quadrilateral, so the outline and the covered area are unchanged.
    /// </summary>
    private static void Flatten(IReadOnlyList<PlanPoint> polygon, List<int[]> triangles)
    {
        var limit = triangles.Count * triangles.Count * 4 + 16;
        var changed = true;
        while (changed && limit-- > 0)
        {
            changed = false;
            for (var i = 0; i < triangles.Count && !changed; i++)
            {
                for (var j = i + 1; j < triangles.Count && !changed; j++)
                {
                    changed = TryFlip(polygon, triangles, i, j);
                }
            }
        }
    }

    private static bool TryFlip(IReadOnlyList<PlanPoint> polygon, List<int[]> triangles, int i, int j)
    {
        var t = triangles[i];
        var u = triangles[j];
        for (var k = 0; k < 3; k++)
        {
            var a = t[k];
            var b = t[(k + 1) % 3];
            var c = t[(k + 2) % 3];

            // the other triangle must run the shared edge the opposite way
            for (var m = 0; m < 3; m++)
            {
                if (u[m] != b || u[(m + 1) % 3] != a)
                {
                    continue;
                }

                var d = u[(m + 2) % 3];
                var pa = polygon[a];
                var pb = polygon[b];
                var pc = polygon[c];
                var pd = polygon[d];

                // the quadrilateral a, d, b, c must be strictly convex at a and b for the other diagonal c-d to lie inside it
                if (Cross(pc, pa, pd) <= 1e-9 || Cross(pd, pb, pc) <= 1e-9)
                {
                    return false;
                }

                if (InCircle(pa, pb, pc, pd) <= 1e-9)
                {
                    return false;
                }

                triangles[i] = new[] { c, a, d };
                triangles[j] = new[] { d, b, c };
                return true;
            }
        }

        return false;
    }

    /// <summary>Positive when <paramref name="d"/> lies inside the circle through the counter-clockwise triangle a, b, c.</summary>
    private static double InCircle(PlanPoint a, PlanPoint b, PlanPoint c, PlanPoint d)
    {
        var ax = a.X - d.X;
        var ay = a.Y - d.Y;
        var bx = b.X - d.X;
        var by = b.Y - d.Y;
        var cx = c.X - d.X;
        var cy = c.Y - d.Y;
        return (ax * ax + ay * ay) * (bx * cy - cx * by)
            - (bx * bx + by * by) * (ax * cy - cx * ay)
            + (cx * cx + cy * cy) * (ax * by - bx * ay);
    }

    private static double Cross(PlanPoint a, PlanPoint b, PlanPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool InsideTriangle(PlanPoint p, PlanPoint a, PlanPoint b, PlanPoint c)
    {
        // a point on an edge of the triangle counts as inside: a vertex on the edge would otherwise leave a T-junction that a closed
        // solid can't have (the outline gets collinear points where the surface is lengthened along a side road kerb line)
        const double eps = 1e-9;
        return Cross(a, b, p) >= -eps && Cross(b, c, p) >= -eps && Cross(c, a, p) >= -eps;
    }
}
