using TopoAlign.Preview;

namespace TopoAlign.Alignments;

/// <summary>
/// Reads the points of intersection (PIs) back out of an alignment's stored plan so they can be edited. The stored plan is only
/// straights and arcs: a transition spiral is a run of short straights (see <see cref="AlignmentJsonDocument.SetPlan"/>), so a
/// spiral is recognised from the heading change it must make: a spiral of length Ls into an arc of radius R turns the heading by
/// exactly Ls / (2R), which picks out the straight where the spiral starts. Pure geometry with no Revit types.
/// </summary>
internal static class PiRecovery
{
    /// <summary>Two straights whose headings differ by less than this (radians) are one straight.</summary>
    private const double CollinearAngle = 1e-6;

    /// <summary>How far (radians) the heading change of a spiral may stray from Ls / (2R).</summary>
    private const double HeadingTolerance = 2e-4;

    /// <summary>Largest gap (feet) between the end of one segment and the start of the next.</summary>
    private const double GapTolerance = 0.01;

    /// <summary>Largest distance (feet) the laid-out PIs may stray from the stored plan.</summary>
    private const double FitTolerance = 0.02;

    /// <summary>
    /// Finds the PIs, radii and spiral lengths of a plan made of straights, arcs and spirals written as straights. Returns false,
    /// with the reason, when the plan isn't a chain of tangent straights joined by sharp corners or symmetrical curves, or when
    /// laying the PIs out again doesn't reproduce the stored plan.
    /// </summary>
    public static bool TryRecover(IReadOnlyList<PlanSegment> plan, out List<PiPoint> pis, out string? reason)
    {
        pis = new List<PiPoint>();
        reason = null;

        if (plan is null || plan.Count == 0)
        {
            reason = "The alignment has no plan geometry.";
            return false;
        }

        for (var i = 1; i < plan.Count; i++)
        {
            if (PlanPoint.Distance(plan[i - 1].EndPoint, plan[i].StartPoint) > GapTolerance)
            {
                reason = "The alignment's plan is not continuous.";
                return false;
            }
        }

        var elements = MergeStraights(plan);
        if (elements[0] is not PlanLine || elements[elements.Count - 1] is not PlanLine)
        {
            reason = "The alignment starts or ends with a curve, so its points can't be edited.";
            return false;
        }

        var consumed = new bool[elements.Count];
        var curves = new Dictionary<int, (int In, int Out, double Radius, double Spiral)>();

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is not PlanArc arc)
            {
                continue;
            }

            var entry = FindSpiral(elements, i, -1, out var entryReason);
            var exit = FindSpiral(elements, i, 1, out var exitReason);
            if (entry is null || exit is null)
            {
                reason = entryReason ?? exitReason;
                return false;
            }

            if (entry.Exists != exit.Exists ||
                (entry.Exists && Math.Abs(entry.Length - exit.Length) > Math.Max(0.05, 0.005 * entry.Length)))
            {
                reason = "A curve has spirals of different lengths, so it can't be edited.";
                return false;
            }

            for (var k = 1; k < entry.TangentOffset; k++)
            {
                consumed[i - k] = true;
            }

            for (var k = 1; k < exit.TangentOffset; k++)
            {
                consumed[i + k] = true;
            }

            curves[i] = (entry.TangentOffset, exit.TangentOffset, arc.Radius, entry.Exists ? (entry.Length + exit.Length) / 2.0 : 0.0);
        }

        // a straight that a spiral grows out of must not itself be part of another spiral
        foreach (var pair in curves)
        {
            if (consumed[pair.Key - pair.Value.In] || consumed[pair.Key + pair.Value.Out])
            {
                reason = "Two curves are too close together for their points to be edited.";
                return false;
            }
        }

        var start = elements[0].StartPoint;
        pis.Add(new PiPoint(start.X, start.Y, 0.0, 0.0));

        var previousWasStraight = false;
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i] is PlanArc)
            {
                var curve = curves[i];
                var before = (PlanLine)elements[i - curve.In];
                var after = (PlanLine)elements[i + curve.Out];
                if (!PiGeometry.TryIntersect(before, after, out var intersection))
                {
                    reason = "The straights either side of a curve do not meet.";
                    return false;
                }

                pis.Add(new PiPoint(intersection.X, intersection.Y, curve.Radius, curve.Spiral));
                previousWasStraight = false;
                continue;
            }

            if (consumed[i])
            {
                continue;
            }

            if (previousWasStraight)
            {
                var corner = elements[i].StartPoint;
                pis.Add(new PiPoint(corner.X, corner.Y, 0.0, 0.0));
            }

            previousWasStraight = true;
        }

        var end = elements[elements.Count - 1].EndPoint;
        pis.Add(new PiPoint(end.X, end.Y, 0.0, 0.0));

        var layout = PiGeometry.Create(pis);
        if (!layout.IsValid)
        {
            reason = "The alignment's curves can't be laid out again from their points: " + layout.Errors[0];
            return false;
        }

        if (!Matches(plan, layout.Segments))
        {
            reason = "The alignment's shape can't be described by points, radii and spirals, so it can't be edited.";
            return false;
        }

        return true;
    }

    /// <summary>Joins straights that run on in the same direction into one.</summary>
    private static List<PlanSegment> MergeStraights(IReadOnlyList<PlanSegment> plan)
    {
        var result = new List<PlanSegment>();
        foreach (var segment in plan)
        {
            if (segment is PlanLine line && result.Count > 0 && result[result.Count - 1] is PlanLine previous &&
                Math.Abs(PiGeometry.NormalizeAngle(line.HeadingAt(0.0) - previous.HeadingAt(0.0))) < CollinearAngle)
            {
                result[result.Count - 1] = new PlanLine(previous.StartPoint, line.EndPoint);
                continue;
            }

            result.Add(segment);
        }

        return result;
    }

    /// <summary>
    /// Looks along the straights before (<paramref name="direction"/> -1) or after (+1) an arc for the one where a transition spiral
    /// starts. The heading change from that straight to the arc must be the spiral's length over twice the radius, so the straight
    /// is the one where that holds best; the straights between it and the arc are the spiral.
    /// </summary>
    private static SpiralDetection? FindSpiral(IReadOnlyList<PlanSegment> elements, int arcIndex, int direction, out string? reason)
    {
        reason = null;
        var arc = (PlanArc)elements[arcIndex];
        var arcHeading = direction < 0 ? arc.HeadingAt(0.0) : arc.HeadingAt(arc.Length);
        var turnSign = arc.Sweep >= 0.0 ? 1.0 : -1.0;

        var chords = 0.0;
        var bestOffset = 0;
        var bestError = double.MaxValue;
        var bestChords = 0.0;
        var bestTurn = 0.0;

        for (var offset = 1; ; offset++)
        {
            var index = arcIndex + direction * offset;
            if (index < 0 || index >= elements.Count || elements[index] is not PlanLine line)
            {
                break;
            }

            // heading change from this straight to the arc, in the direction of travel, positive when it turns the arc's way
            var turn = direction < 0
                ? PiGeometry.NormalizeAngle(arcHeading - line.HeadingAt(0.0)) * turnSign
                : PiGeometry.NormalizeAngle(line.HeadingAt(0.0) - arcHeading) * turnSign;

            if (turn > -1e-9)
            {
                var error = Math.Abs(turn - chords / (2.0 * arc.Radius));
                if (error < bestError)
                {
                    bestError = error;
                    bestOffset = offset;
                    bestChords = chords;
                    bestTurn = turn;
                }
            }

            chords += line.Length;
            if (chords / (2.0 * arc.Radius) > Math.PI)
            {
                break;
            }
        }

        if (bestOffset == 0)
        {
            reason = "A curve has no straight beside it, so its points can't be edited.";
            return null;
        }

        if (bestError > HeadingTolerance)
        {
            reason = "A curve is not tangent to the straights beside it, so its points can't be edited.";
            return null;
        }

        // a straight that runs straight into the arc has no spiral
        return bestOffset == 1 && bestChords <= 0.0
            ? new SpiralDetection(1, 0.0)
            : new SpiralDetection(bestOffset, 2.0 * arc.Radius * Math.Max(bestTurn, 0.0));
    }

    /// <summary>True if the two chains of segments follow the same path to within <see cref="FitTolerance"/> and are the same length.</summary>
    private static bool Matches(IReadOnlyList<PlanSegment> stored, IReadOnlyList<PlanSegment> laidOut)
    {
        var lengthStored = PlanSegment.TotalLength(stored);
        var lengthLaidOut = PlanSegment.TotalLength(laidOut);
        if (Math.Abs(lengthStored - lengthLaidOut) > Math.Max(0.01, 0.001 * lengthStored))
        {
            return false;
        }

        var a = PlanPolyline.FromSegments(stored, 0.005);
        var b = PlanPolyline.FromSegments(laidOut, 0.005);
        return Within(a, b) && Within(b, a);
    }

    /// <summary>True if every point of <paramref name="points"/> is within <see cref="FitTolerance"/> of the polyline <paramref name="path"/>.</summary>
    private static bool Within(IReadOnlyList<PlanPoint> points, IReadOnlyList<PlanPoint> path)
    {
        foreach (var point in points)
        {
            var nearest = double.MaxValue;
            for (var i = 0; i + 1 < path.Count && nearest > FitTolerance; i++)
            {
                nearest = Math.Min(nearest, DistanceToSegment(point, path[i], path[i + 1]));
            }

            if (path.Count == 1)
            {
                nearest = PlanPoint.Distance(point, path[0]);
            }

            if (nearest > FitTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static double DistanceToSegment(PlanPoint p, PlanPoint a, PlanPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 1e-18)
        {
            return PlanPoint.Distance(p, a);
        }

        var t = Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared));
        return PlanPoint.Distance(p, new PlanPoint(a.X + dx * t, a.Y + dy * t));
    }
}
