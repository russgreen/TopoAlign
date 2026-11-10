using TopoAlign.Grading.TopoEdge;

namespace TopoAlign.Grading;

/// <summary>Sorts points against a toposolid's plan outline. Works on plain coordinates, so it needs no Revit geometry.</summary>
internal static class BoundaryPoints
{
    /// <summary>
    /// Sorts the points (in plan) against the outline: at a corner, on an edge, outside, or inside. A corner is only reported once;
    /// the first point at it wins. The outline may be unordered and may include holes.
    /// </summary>
    public static BoundarySplit Split(IEnumerable<GradePoint> points, IReadOnlyList<EdgeSegment> outline, double tolerance = TopoEdgeAligner.DefaultPlanTolerance)
    {
        if (points is null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (outline is null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        var split = new BoundarySplit();
        foreach (var point in points)
        {
            if (outline.Count == 0)
            {
                split.Add.Add(point);
            }
            else if (AtCorner(point, outline, tolerance))
            {
                if (!split.MoveCorner.Any(moved => PlanMath.Distance(moved, point) <= 2.0 * tolerance))
                {
                    split.MoveCorner.Add(point);
                }
            }
            else if (OnOutline(point, outline, tolerance))
            {
                split.OnEdge.Add(point);
            }
            else if (!Inside(point, outline))
            {
                split.Outside.Add(point);
            }
            else
            {
                split.Add.Add(point);
            }
        }

        return split;
    }

    /// <summary>How far (in plan) the point is from the nearest part of the outline; infinity if there is no outline.</summary>
    public static double DistanceToOutline(GradePoint point, IReadOnlyList<EdgeSegment> outline)
    {
        var best = double.PositiveInfinity;
        foreach (var segment in outline)
        {
            best = Math.Min(best, DistanceToSegment(point, segment));
        }

        return best;
    }

    /// <summary>The nearest point on the outline, in plan, keeping the given point's height.</summary>
    public static bool TryProjectOntoOutline(GradePoint point, IReadOnlyList<EdgeSegment> outline, out GradePoint projected)
    {
        return TryNearest(point, outline, out projected, out _);
    }

    /// <summary>The point moved to the inside of the outline by the given distance (in plan) from the nearest outline edge, keeping its height.</summary>
    public static bool TryInsetFromOutline(GradePoint point, IReadOnlyList<EdgeSegment> outline, double distance, out GradePoint inset)
    {
        inset = default;
        if (!TryNearest(point, outline, out var projected, out var segment))
        {
            return false;
        }

        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-12)
        {
            return false;
        }

        var nx = -dy / length;
        var ny = dx / length;
        foreach (var sign in new[] { 1.0, -1.0 })
        {
            var candidate = new GradePoint(projected.X + sign * nx * distance, projected.Y + sign * ny * distance, point.Z);
            if (Inside(candidate, outline) && DistanceToOutline(candidate, outline) >= distance * 0.9)
            {
                inset = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryNearest(GradePoint point, IReadOnlyList<EdgeSegment> outline, out GradePoint projected, out EdgeSegment nearest)
    {
        projected = default;
        nearest = default;
        var best = double.PositiveInfinity;
        foreach (var segment in outline)
        {
            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared < 1e-18
                ? 0.0
                : Math.Max(0.0, Math.Min(1.0, ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared));
            var cx = segment.Start.X + t * dx;
            var cy = segment.Start.Y + t * dy;
            var distance = Math.Sqrt((point.X - cx) * (point.X - cx) + (point.Y - cy) * (point.Y - cy));
            if (distance < best)
            {
                best = distance;
                projected = new GradePoint(cx, cy, point.Z);
                nearest = segment;
            }
        }

        return !double.IsPositiveInfinity(best);
    }

    private static bool AtCorner(GradePoint point, IReadOnlyList<EdgeSegment> outline, double tolerance)
    {
        foreach (var segment in outline)
        {
            if (PlanMath.Distance(point, segment.Start) <= tolerance || PlanMath.Distance(point, segment.End) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OnOutline(GradePoint point, IReadOnlyList<EdgeSegment> outline, double tolerance)
    {
        foreach (var segment in outline)
        {
            if (DistanceToSegment(point, segment) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Even-odd test against every outline segment, so a hole in the toposolid counts as outside.</summary>
    private static bool Inside(GradePoint point, IReadOnlyList<EdgeSegment> outline)
    {
        var inside = false;
        foreach (var segment in outline)
        {
            var a = segment.Start;
            var b = segment.End;
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double DistanceToSegment(GradePoint point, EdgeSegment segment)
    {
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared < 1e-18
            ? 0.0
            : Math.Max(0.0, Math.Min(1.0, ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared));
        var cx = segment.Start.X + t * dx;
        var cy = segment.Start.Y + t * dy;
        return Math.Sqrt((point.X - cx) * (point.X - cx) + (point.Y - cy) * (point.Y - cy));
    }
}