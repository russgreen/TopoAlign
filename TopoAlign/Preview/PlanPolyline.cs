using TopoAlign.Alignments;

namespace TopoAlign.Preview;

/// <summary>
/// Turns plan geometry into plain point lists for the pick preview. Pure geometry with no Revit types, so it can be tested
/// without a model.
/// </summary>
internal static class PlanPolyline
{
    private const int MaxPiecesPerArc = 720;

    /// <summary>
    /// Points along a chain of segments, with straights as two points and arcs and spirals sampled so no chord strays more than
    /// <paramref name="maxDeviation"/> (feet) from the curve. The chain is expected to be continuous, so the result is one polyline.
    /// </summary>
    public static List<PlanPoint> FromSegments(IReadOnlyList<PlanSegment> segments, double maxDeviation)
    {
        var points = new List<PlanPoint>();
        if (segments is null)
        {
            return points;
        }

        foreach (var segment in segments)
        {
            foreach (var point in Sample(segment, maxDeviation))
            {
                if (points.Count == 0 || PlanPoint.Distance(points[points.Count - 1], point) > 1e-9)
                {
                    points.Add(point);
                }
            }
        }

        return points;
    }

    /// <summary>The straight tangents through the points of intersection, without any curves.</summary>
    public static List<PlanPoint> Through(IReadOnlyList<PiPoint> pis)
    {
        var points = new List<PlanPoint>(pis?.Count ?? 0);
        if (pis is null)
        {
            return points;
        }

        foreach (var pi in pis)
        {
            points.Add(new PlanPoint(pi.X, pi.Y));
        }

        return points;
    }

    /// <summary>The two ends of each arm of a small cross centred on each point, <paramref name="size"/> feet from tip to tip.</summary>
    public static List<(PlanPoint From, PlanPoint To)> Crosses(IReadOnlyList<PiPoint> pis, double size)
    {
        var arms = new List<(PlanPoint, PlanPoint)>();
        if (pis is null)
        {
            return arms;
        }

        var half = size / 2.0;
        foreach (var pi in pis)
        {
            arms.Add((new PlanPoint(pi.X - half, pi.Y), new PlanPoint(pi.X + half, pi.Y)));
            arms.Add((new PlanPoint(pi.X, pi.Y - half), new PlanPoint(pi.X, pi.Y + half)));
        }

        return arms;
    }

    private static IEnumerable<PlanPoint> Sample(PlanSegment segment, double maxDeviation)
    {
        switch (segment)
        {
            case PlanSpiral spiral:
                return spiral.Tessellate(maxDeviation);

            case PlanArc arc:
                return SampleArc(arc, maxDeviation);

            default:
                return new[] { segment.StartPoint, segment.EndPoint };
        }
    }

    private static List<PlanPoint> SampleArc(PlanArc arc, double maxDeviation)
    {
        // a chord of angle a deviates from the arc by r * (1 - cos(a / 2))
        var tolerance = Math.Max(maxDeviation, 1e-6);
        var ratio = Math.Min(1.0, tolerance / arc.Radius);
        var maxAngle = 2.0 * Math.Acos(1.0 - ratio);
        var pieces = maxAngle <= 1e-9 ? MaxPiecesPerArc : (int)Math.Ceiling(Math.Abs(arc.Sweep) / maxAngle);
        pieces = Math.Max(1, Math.Min(pieces, MaxPiecesPerArc));

        var points = new List<PlanPoint>(pieces + 1);
        for (var i = 0; i <= pieces; i++)
        {
            points.Add(arc.PointAt(arc.Length * i / pieces));
        }

        return points;
    }
}
