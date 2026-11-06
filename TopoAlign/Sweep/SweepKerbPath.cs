using Autodesk.Revit.DB;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>One continuous run of picked edges, sampled into points.</summary>
internal sealed class KerbChain
{
    public KerbChain(List<PathPoint> points, bool closed, double length, int curveCount)
    {
        Points = points;
        Closed = closed;
        Length = length;
        CurveCount = curveCount;
    }

    /// <summary>The path in order. For a closed loop the first point is not repeated at the end.</summary>
    public List<PathPoint> Points { get; }

    public bool Closed { get; }

    /// <summary>Length along the curves in feet.</summary>
    public double Length { get; }

    public int CurveCount { get; }
}

/// <summary>Turns the curves of picked edges into the runs a kerb follows.</summary>
internal static class SweepKerbPath
{
    /// <summary>Curves whose ends are this close (feet) are joined; the same tolerance the curve chainer uses.</summary>
    private const double JoinTolerance = 1.0e-4;

    /// <summary>Largest distance a curve may bend away from a straight chord between two sections, in feet (about 6 mm).</summary>
    private const double ChordTolerance = 0.02;

    /// <summary>Drops a curve that repeats an earlier one (the same edge picked twice, or two faces sharing it).</summary>
    public static List<Curve> RemoveDuplicates(IEnumerable<Curve> curves)
    {
        var result = new List<Curve>();
        foreach (var curve in curves)
        {
            if (curve is null || !curve.IsBound)
            {
                continue;
            }

            if (!result.Any(other => SameCurve(curve, other)))
            {
                result.Add(curve);
            }
        }

        return result;
    }

    private static bool SameCurve(Curve a, Curve b)
    {
        if (Math.Abs(a.Length - b.Length) > JoinTolerance * 10)
        {
            return false;
        }

        var a0 = a.GetEndPoint(0);
        var a1 = a.GetEndPoint(1);
        var b0 = b.GetEndPoint(0);
        var b1 = b.GetEndPoint(1);
        var ends = (a0.DistanceTo(b0) <= JoinTolerance && a1.DistanceTo(b1) <= JoinTolerance)
                   || (a0.DistanceTo(b1) <= JoinTolerance && a1.DistanceTo(b0) <= JoinTolerance);
        return ends && a.Evaluate(0.5, true).DistanceTo(b.Evaluate(0.5, true)) <= JoinTolerance * 10;
    }

    /// <summary>
    /// Joins the curves into continuous runs (curves that don't touch make separate runs) and samples each into points.
    /// An open run keeps the direction of its first curve as picked; a closed loop goes anticlockwise in plan, so a
    /// profile's +u (the right of the direction of travel) is on the outside of the loop.
    /// </summary>
    /// <param name="interval">Longest distance between sections on a curved edge, in feet.</param>
    /// <param name="error">Why the curves can't be followed, or null.</param>
    public static List<KerbChain> Build(IEnumerable<Curve> curves, double interval, out string? error)
    {
        error = null;
        var chains = new List<KerbChain>();
        var unique = RemoveDuplicates(curves);
        if (unique.Count == 0)
        {
            error = "No edges or lines were picked.";
            return chains;
        }

        foreach (var group in Components(unique))
        {
            var chain = CurveChainer.TryChain(group, projectToPlan: false);
            if (!chain.IsContinuous)
            {
                error = chain.ErrorMessage == "Selected curves branch at one or more points."
                    ? "The picked edges branch. Pick one run of edges at a time, with no more than two edges meeting at any point."
                    : chain.ErrorMessage ?? "The picked edges could not be joined.";
                return new List<KerbChain>();
            }

            var closed = chain.Curves.Count > 1
                         && chain.Curves[0].GetEndPoint(0).DistanceTo(chain.Curves[chain.Curves.Count - 1].GetEndPoint(1)) <= JoinTolerance;

            var points = Sample(chain.Curves, interval);
            if (closed)
            {
                // the last point repeats the first
                points.RemoveAt(points.Count - 1);
                if (SignedArea(points) < 0)
                {
                    points.Reverse();
                }
            }

            chains.Add(new KerbChain(points, closed, chain.TotalLength, chain.Curves.Count));
        }

        return chains;
    }

    /// <summary>Groups curves whose ends touch.</summary>
    private static List<List<Curve>> Components(IReadOnlyList<Curve> curves)
    {
        var parent = Enumerable.Range(0, curves.Count).ToArray();
        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        for (var i = 0; i < curves.Count; i++)
        {
            for (var j = i + 1; j < curves.Count; j++)
            {
                if (Touch(curves[i], curves[j]))
                {
                    parent[Find(i)] = Find(j);
                }
            }
        }

        // keep the order the curves were picked in, so the first pick decides the direction
        var groups = new List<List<Curve>>();
        var index = new Dictionary<int, int>();
        for (var i = 0; i < curves.Count; i++)
        {
            var root = Find(i);
            if (!index.TryGetValue(root, out var slot))
            {
                slot = groups.Count;
                index[root] = slot;
                groups.Add(new List<Curve>());
            }

            groups[slot].Add(curves[i]);
        }

        return groups;
    }

    private static bool Touch(Curve a, Curve b)
    {
        foreach (var i in new[] { 0, 1 })
        {
            foreach (var j in new[] { 0, 1 })
            {
                if (a.GetEndPoint(i).DistanceTo(b.GetEndPoint(j)) <= JoinTolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The points of a chain in order, with curved edges cut into short enough pieces; the first point of each curve is the last of the one before.</summary>
    internal static List<PathPoint> Sample(IReadOnlyList<Curve> curves, double interval)
    {
        var step = Math.Max(interval, 0.1);
        var points = new List<PathPoint>();
        for (var c = 0; c < curves.Count; c++)
        {
            var curve = curves[c];
            if (c == 0)
            {
                points.Add(ToPoint(curve.GetEndPoint(0)));
            }

            if (curve is not Line)
            {
                var length = curve.Length;
                var count = Math.Max(2, (int)Math.Ceiling(length / step));
                var parameters = StationPlanner.Plan(0.0, 1.0, 1.0 / count);
                parameters = StationPlanner.Refine(
                    parameters,
                    t => ToPoint(curve.Evaluate(t, true)),
                    ChordTolerance,
                    Math.Max(0.25 / Math.Max(length, 0.25), 1.0 / 4096.0));

                // the first parameter is the point already added
                for (var i = 1; i < parameters.Count - 1; i++)
                {
                    points.Add(ToPoint(curve.Evaluate(parameters[i], true)));
                }
            }

            points.Add(ToPoint(curve.GetEndPoint(1)));
        }

        return points;
    }

    private static PathPoint ToPoint(XYZ point) => new(point.X, point.Y, point.Z);

    /// <summary>Positive when the points go anticlockwise in plan.</summary>
    internal static double SignedArea(IReadOnlyList<PathPoint> points)
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
}
