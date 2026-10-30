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

        var curves = sourceCurves
            .Where(c => c is not null)
            .Select(ProjectToPlan)
            .ToList();

        if (curves.Count == 0)
        {
            result.ErrorMessage = "No supported curves selected.";
            return result;
        }

        if (curves.Any(c => c is not Line and not Arc))
        {
            result.ErrorMessage = "Only line and arc curves are supported.";
            return result;
        }

        var startIndex = FindStartCurveIndex(curves);

        var used = new HashSet<int>();
        var current = curves[startIndex];
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

    private static int FindStartCurveIndex(IReadOnlyList<Curve> curves)
    {
        for (var i = 0; i < curves.Count; i++)
        {
            var start = curves[i].GetEndPoint(0);
            var touches = 0;

            for (var j = 0; j < curves.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var candidate = curves[j];
                if (candidate.GetEndPoint(0).DistanceTo(start) <= Tolerance || candidate.GetEndPoint(1).DistanceTo(start) <= Tolerance)
                {
                    touches++;
                }
            }

            if (touches == 0)
            {
                return i;
            }
        }

        return 0;
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
