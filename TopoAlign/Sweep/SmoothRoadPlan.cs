using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>Pure helpers that decide how a road is cut into smooth pieces and what a section looks like as a ring of vertices.</summary>
internal static class SmoothRoadPlan
{
    /// <summary>Largest angle one straight facet of a rounded section may turn through, in radians (5 degrees).</summary>
    private const double ArcStep = Math.PI / 36.0;

    /// <summary>Vertices closer together than this (in feet) are treated as one.</summary>
    private const double MergeDistance = 0.005;

    /// <summary>
    /// Splits a run of stations into pieces that each end where a horizontal curve ends, so every piece is one kind of
    /// geometry and its splines stay accurate. Pieces shorter than <paramref name="minLength"/> are merged into a neighbour
    /// (spirals are many tiny straights). Each range is (first, last) station indexes; neighbouring ranges share an index.
    /// </summary>
    public static List<(int First, int Last)> Chunks(IReadOnlyList<double> stations, IEnumerable<double> joins, double minLength)
    {
        var result = new List<(int First, int Last)>();
        if (stations is null || stations.Count < 2)
        {
            return result;
        }

        var boundaries = new List<int> { 0 };
        var joinList = (joins ?? Enumerable.Empty<double>()).OrderBy(j => j).ToList();
        var last = stations.Count - 1;
        for (var i = 1; i < last; i++)
        {
            if (!joinList.Any(j => Math.Abs(j - stations[i]) < 1e-6))
            {
                continue;
            }

            if (stations[i] - stations[boundaries[boundaries.Count - 1]] >= minLength && stations[last] - stations[i] >= minLength)
            {
                boundaries.Add(i);
            }
        }

        boundaries.Add(last);
        for (var i = 0; i < boundaries.Count - 1; i++)
        {
            result.Add((boundaries[i], boundaries[i + 1]));
        }

        return result;
    }

    /// <summary>
    /// The corners of a section as a ring, anticlockwise when drawn with u to the right and v up. Rounded parts are
    /// cut into short straights (a smooth surface between two splines can only be straight across the section).
    /// </summary>
    public static List<PlanPoint> Ring(SectionProfile profile)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var points = new List<PlanPoint>();

        void Add(PlanPoint point)
        {
            if (points.Count == 0 || PlanPoint.Distance(points[points.Count - 1], point) > MergeDistance)
            {
                points.Add(point);
            }
        }

        foreach (var segment in profile.Segments)
        {
            Add(segment.StartPoint);
            if (segment is PlanArc arc)
            {
                var pieces = Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.Sweep) / ArcStep));
                for (var i = 1; i < pieces; i++)
                {
                    Add(arc.PointAt(arc.Length * i / pieces));
                }
            }
        }

        while (points.Count > 1 && PlanPoint.Distance(points[0], points[points.Count - 1]) <= MergeDistance)
        {
            points.RemoveAt(points.Count - 1);
        }

        if (SignedArea(points) < 0)
        {
            points.Reverse();
        }

        return points;
    }

    /// <summary>Positive when the points run anticlockwise.</summary>
    public static double SignedArea(IReadOnlyList<PlanPoint> points)
    {
        var twice = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            twice += a.X * b.Y - b.X * a.Y;
        }

        return twice / 2.0;
    }

    /// <summary>
    /// The largest bend of the path per foot (1 / smallest radius) seen between consecutive stations, from the change of
    /// heading. Used to tell when a section is wider than the tightest turn can take.
    /// </summary>
    public static double MaxCurvature(IReadOnlyList<double> stations, IReadOnlyList<(double X, double Y)> tangents)
    {
        var max = 0.0;
        for (var i = 1; i < stations.Count && i < tangents.Count; i++)
        {
            var (ax, ay) = tangents[i - 1];
            var (bx, by) = tangents[i];
            var turn = Math.Abs(Math.Atan2(ax * by - ay * bx, ax * bx + ay * by));
            var distance = stations[i] - stations[i - 1];
            if (distance > 1e-9)
            {
                max = Math.Max(max, turn / distance);
            }
        }

        return max;
    }
}
