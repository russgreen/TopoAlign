using Autodesk.Revit.DB;

namespace TopoAlign.Alignments;

internal static class CurveChainer
{
    private const double Tolerance = 1.0e-4;

    internal sealed class ChainResult
    {
        public List<Curve> Curves { get; } = new();

        public double TotalLength { get; set; }

        public string ErrorMessage { get; set; }

        public bool IsContinuous => Curves.Count > 0 && string.IsNullOrEmpty(ErrorMessage);
    }

    public static ChainResult TryChain(IEnumerable<Curve> sourceCurves)
    {
        var result = new ChainResult();

        if (sourceCurves is null)
        {
            result.ErrorMessage = "No curves selected.";
            return result;
        }

        var picked = sourceCurves
            .Where(c => c is not null)
            .ToList();

        if (picked.Count == 0)
        {
            result.ErrorMessage = "No supported curves selected.";
            return result;
        }

        // Check the types before projecting, because projecting only knows how to rebuild lines and arcs.
        if (picked.Any(c => c is not Line and not Arc))
        {
            result.ErrorMessage = "Only line and arc curves are supported.";
            return result;
        }

        var curves = picked
            .Select(ProjectToPlan)
            .ToList();

        var (startIndex, reverseStart) = FindStartCurve(curves);

        var used = new HashSet<int>();
        var current = reverseStart ? ReverseCurve(curves[startIndex]) : curves[startIndex];
        used.Add(startIndex);
        result.Curves.Add(current);

        while (used.Count < curves.Count)
        {
            var currentEnd = current.GetEndPoint(1);

            var matches = new List<(int Index, bool Reverse)>();
            for (var i = 0; i < curves.Count; i++)
            {
                if (used.Contains(i))
                {
                    continue;
                }

                var candidate = curves[i];
                if (candidate.GetEndPoint(0).DistanceTo(currentEnd) <= Tolerance)
                {
                    matches.Add((i, false));
                }
                else if (candidate.GetEndPoint(1).DistanceTo(currentEnd) <= Tolerance)
                {
                    matches.Add((i, true));
                }
            }

            if (matches.Count == 0)
            {
                result.ErrorMessage = "Selected curves are not continuous.";
                result.Curves.Clear();
                result.TotalLength = 0;
                return result;
            }

            if (matches.Count > 1)
            {
                result.ErrorMessage = "Selected curves branch at one or more points.";
                result.Curves.Clear();
                result.TotalLength = 0;
                return result;
            }

            var match = matches[0];
            current = match.Reverse ? ReverseCurve(curves[match.Index]) : curves[match.Index];
            used.Add(match.Index);
            result.Curves.Add(current);
        }

        result.TotalLength = result.Curves.Sum(c => c.Length);
        return result;
    }

    public static List<Curve> ReverseChain(IReadOnlyList<Curve> curves)
    {
        var reversed = new List<Curve>(curves.Count);
        for (var i = curves.Count - 1; i >= 0; i--)
        {
            reversed.Add(ReverseCurve(curves[i]));
        }

        return reversed;
    }

    /// <summary>
    /// Finds the curve the chain should start from: one with a free end (an end no other curve touches).
    /// If that free end is the curve's end point, the curve has to be reversed so the chain leaves from it.
    /// A closed loop has no free ends, so it starts from the first curve as drawn.
    /// </summary>
    private static (int Index, bool Reverse) FindStartCurve(IReadOnlyList<Curve> curves)
    {
        // Prefer a curve whose start is free and end is joined, so the drawn direction is kept where possible
        // and a lone detached curve is not picked over a real chain end.
        for (var i = 0; i < curves.Count; i++)
        {
            if (!TouchesAnother(curves, i, 0) && TouchesAnother(curves, i, 1))
            {
                return (i, false);
            }
        }

        // Otherwise a curve whose end is the free end: it has to be reversed to start the chain.
        for (var i = 0; i < curves.Count; i++)
        {
            if (TouchesAnother(curves, i, 0) && !TouchesAnother(curves, i, 1))
            {
                return (i, true);
            }
        }

        // A single curve, or curves that are not joined at all (reported as not continuous later).
        for (var i = 0; i < curves.Count; i++)
        {
            if (!TouchesAnother(curves, i, 0))
            {
                return (i, false);
            }
        }

        return (0, false);
    }

    private static bool TouchesAnother(IReadOnlyList<Curve> curves, int index, int endIndex)
    {
        var point = curves[index].GetEndPoint(endIndex);

        for (var j = 0; j < curves.Count; j++)
        {
            if (j == index)
            {
                continue;
            }

            var candidate = curves[j];
            if (candidate.GetEndPoint(0).DistanceTo(point) <= Tolerance || candidate.GetEndPoint(1).DistanceTo(point) <= Tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static Curve ProjectToPlan(Curve curve)
    {
        var start = curve.GetEndPoint(0);
        var end = curve.GetEndPoint(1);
        var p0 = new XYZ(start.X, start.Y, 0.0);
        var p1 = new XYZ(end.X, end.Y, 0.0);

        if (curve is Line)
        {
            return Line.CreateBound(p0, p1);
        }

        if (curve is Arc)
        {
            var mid = curve.Evaluate(0.5, true);
            var pm = new XYZ(mid.X, mid.Y, 0.0);
            return Arc.Create(p0, p1, pm);
        }

        throw new NotSupportedException($"Unsupported curve type '{curve.GetType().Name}'.");
    }

    private static Curve ReverseCurve(Curve curve)
    {
        if (curve is Line)
        {
            return Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0));
        }

        if (curve is Arc)
        {
            return Arc.Create(curve.GetEndPoint(1), curve.GetEndPoint(0), curve.Evaluate(0.5, true));
        }

        throw new NotSupportedException($"Unsupported curve type '{curve.GetType().Name}'.");
    }
}
