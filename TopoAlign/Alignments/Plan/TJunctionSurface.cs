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
    private const double MaxArcStep = Math.PI / 12.0; // 15 degrees

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

        // left return, from the side road kerb to the main road kerb
        AddArc(points, result.KerbLeft, sideElevation(result.KerbLeft.StartPoint), mainElevation(result.KerbLeft.EndPoint), false);

        // main road kerb line is a straight chord between the ends of the two returns; its elevation follows the main road
        // right return, from the main road kerb back to the side road kerb
        AddArc(points, result.KerbRight, sideElevation(result.KerbRight.StartPoint), mainElevation(result.KerbRight.EndPoint), true);

        // the closing edge runs from the end of the right return back to the start of the left return, across the side road

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
        var steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(arc.Sweep) / MaxArcStep));
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

        return triangles;
    }

    private static double Cross(PlanPoint a, PlanPoint b, PlanPoint c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool InsideTriangle(PlanPoint p, PlanPoint a, PlanPoint b, PlanPoint c)
    {
        const double eps = 1e-9;
        return Cross(a, b, p) > eps && Cross(b, c, p) > eps && Cross(c, a, p) > eps;
    }
}
