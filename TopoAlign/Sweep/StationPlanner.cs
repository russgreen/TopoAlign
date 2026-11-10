namespace TopoAlign.Sweep;

/// <summary>
/// Chooses the stations along an alignment where a cross-section is placed: evenly spaced, at the places the geometry
/// changes kind, and extra ones wherever the path bends faster than the spacing can follow. Pure; no Revit types.
/// </summary>
internal static class StationPlanner
{
    private const double Epsilon = 1e-6;

    /// <summary>
    /// Stations from <paramref name="start"/> to <paramref name="end"/> inclusive: every <paramref name="interval"/>, plus
    /// the <paramref name="mandatory"/> stations (for example the joins between straights and arcs) that lie in between.
    /// A regular station that lands within a tenth of an interval of a mandatory one or of the end is dropped, so there are
    /// no slivers between sections.
    /// </summary>
    public static List<double> Plan(double start, double end, double interval, IEnumerable<double>? mandatory = null)
    {
        if (!(end > start))
        {
            throw new ArgumentException("The end station must be after the start station.", nameof(end));
        }

        var step = Math.Max(interval, Math.Max((end - start) / 100000.0, 1e-3));
        var required = new List<double> { start, end };
        if (mandatory is not null)
        {
            required.AddRange(mandatory.Where(s => s > start + Epsilon && s < end - Epsilon));
        }

        required = Dedupe(required.OrderBy(s => s).ToList(), Epsilon);

        var regular = new List<double>();
        for (var k = 1; start + k * step < end - Epsilon; k++)
        {
            regular.Add(start + k * step);
        }

        var minimumGap = 0.1 * step;
        var stations = new List<double>(required);
        foreach (var station in regular)
        {
            if (required.All(r => Math.Abs(r - station) >= minimumGap))
            {
                stations.Add(station);
            }
        }

        return Dedupe(stations.OrderBy(s => s).ToList(), Epsilon);
    }

    /// <summary>
    /// Adds stations between the given ones wherever the middle of a span sits more than <paramref name="chordTolerance"/>
    /// feet off the straight line between its ends, down to spans of <paramref name="minSpacing"/>. This follows tight
    /// curves and crests or sags in the profile without making a long straight run dense.
    /// </summary>
    public static List<double> Refine(IReadOnlyList<double> stations, Func<double, PathPoint> pointAt, double chordTolerance, double minSpacing, int maxDepth = 8)
    {
        if (stations is null)
        {
            throw new ArgumentNullException(nameof(stations));
        }

        if (pointAt is null)
        {
            throw new ArgumentNullException(nameof(pointAt));
        }

        var result = new List<double>(stations.Count);
        if (stations.Count == 0)
        {
            return result;
        }

        var cache = new Dictionary<double, PathPoint>();
        PathPoint At(double station)
        {
            if (!cache.TryGetValue(station, out var point))
            {
                point = pointAt(station);
                cache[station] = point;
            }

            return point;
        }

        result.Add(stations[0]);
        for (var i = 1; i < stations.Count; i++)
        {
            Split(stations[i - 1], stations[i], 0);
            result.Add(stations[i]);
        }

        return result;

        void Split(double a, double b, int depth)
        {
            if (depth >= maxDepth || (b - a) / 2.0 < Math.Max(minSpacing, Epsilon))
            {
                return;
            }

            var middle = (a + b) / 2.0;
            var pa = At(a);
            var pb = At(b);
            var chordMiddle = new PathPoint((pa.X + pb.X) / 2.0, (pa.Y + pb.Y) / 2.0, (pa.Z + pb.Z) / 2.0);
            if (PathPoint.Distance(At(middle), chordMiddle) <= chordTolerance)
            {
                return;
            }

            Split(a, middle, depth + 1);
            result.Add(middle);
            Split(middle, b, depth + 1);
        }
    }

    private static List<double> Dedupe(List<double> sorted, double tolerance)
    {
        var result = new List<double>(sorted.Count);
        foreach (var station in sorted)
        {
            if (result.Count == 0 || station - result[result.Count - 1] > tolerance)
            {
                result.Add(station);
            }
        }

        return result;
    }
}