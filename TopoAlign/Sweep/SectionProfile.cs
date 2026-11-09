using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>
/// The cross-section of a road or path: one closed loop of straights and arcs in (u, v) feet, where u runs across the
/// road (positive to the right of the direction of travel) and v runs up. The origin is the centreline at finished level.
/// Pure geometry, built on the same plan segments as the alignment layout.
/// </summary>
internal sealed class SectionProfile
{
    /// <summary>Largest gap, in feet, allowed between one segment's end and the next one's start.</summary>
    private const double ClosureTolerance = 1e-4;

    private const int ArcSamples = 24;

    private SectionProfile(IReadOnlyList<PlanSegment> segments)
    {
        Segments = segments;

        var polygon = Outline();
        MinU = polygon.Min(p => p.X);
        MaxU = polygon.Max(p => p.X);
        MinV = polygon.Min(p => p.Y);
        MaxV = polygon.Max(p => p.Y);

        var twiceArea = 0.0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            twiceArea += a.X * b.Y - b.X * a.Y;
        }

        Area = Math.Abs(twiceArea) / 2.0;
    }

    /// <summary>The curves of the loop in order; each ends where the next begins and the last ends where the first begins.</summary>
    public IReadOnlyList<PlanSegment> Segments { get; }

    public double MinU { get; }
    public double MaxU { get; }
    public double MinV { get; }
    public double MaxV { get; }

    public double Width => MaxU - MinU;
    public double Height => MaxV - MinV;

    /// <summary>Area enclosed by the loop in square feet.</summary>
    public double Area { get; }

    /// <summary>
    /// Makes a section from a loop of straights and arcs. False, with the reason, if it isn't a closed loop or encloses nothing.
    /// </summary>
    public static bool TryCreate(IReadOnlyList<PlanSegment> segments, out SectionProfile? profile, out string? error)
    {
        profile = null;
        error = null;

        if (segments is null || segments.Count < 2)
        {
            error = "The profile needs at least two curves forming a closed shape.";
            return false;
        }

        if (segments.Any(s => s is PlanSpiral))
        {
            error = "The profile can only contain straight lines and arcs.";
            return false;
        }

        if (segments.Any(s => s.Length < 1e-6))
        {
            error = "The profile contains a curve that is too short.";
            return false;
        }

        for (var i = 0; i < segments.Count; i++)
        {
            var next = segments[(i + 1) % segments.Count];
            if (PlanPoint.Distance(segments[i].EndPoint, next.StartPoint) > ClosureTolerance)
            {
                error = i == segments.Count - 1
                    ? "The profile is not a closed shape: its last curve does not meet its first."
                    : "The profile curves are not connected end to end.";
                return false;
            }
        }

        var candidate = new SectionProfile(segments);
        if (candidate.Area < 1e-6)
        {
            error = "The profile does not enclose any area.";
            return false;
        }

        profile = candidate;
        return true;
    }

    /// <summary>The same section flipped left to right (u becomes -u). Useful when a profile family is drawn the other way round.</summary>
    public SectionProfile Mirrored()
    {
        var flipped = new List<PlanSegment>(Segments.Count);
        foreach (var segment in Segments)
        {
            switch (segment)
            {
                case PlanLine line:
                    flipped.Add(new PlanLine(Flip(line.StartPoint), Flip(line.EndPoint)));
                    break;

                case PlanArc arc:
                    flipped.Add(new PlanArc(Flip(arc.Center), arc.Radius, Math.PI - arc.StartAngle, -arc.Sweep));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported segment type '{segment.GetType().Name}'.");
            }
        }

        return new SectionProfile(flipped);
    }

    /// <summary>
    /// The same section moved across the road by <paramref name="du"/> (positive to the right of the direction of travel) and
    /// up by <paramref name="dv"/>, in feet. Used to position one part of an assembly relative to the centreline.
    /// </summary>
    public SectionProfile Offset(double du, double dv)
    {
        var moved = new List<PlanSegment>(Segments.Count);
        foreach (var segment in Segments)
        {
            switch (segment)
            {
                case PlanLine line:
                    moved.Add(new PlanLine(Move(line.StartPoint, du, dv), Move(line.EndPoint, du, dv)));
                    break;

                case PlanArc arc:
                    moved.Add(new PlanArc(Move(arc.Center, du, dv), arc.Radius, arc.StartAngle, arc.Sweep));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported segment type '{segment.GetType().Name}'.");
            }
        }

        return new SectionProfile(moved);
    }

    /// <summary>The points of the loop in order, with arcs sampled finely enough to measure the shape.</summary>
    public List<PlanPoint> Outline()
    {
        var points = new List<PlanPoint>();
        foreach (var segment in Segments)
        {
            points.Add(segment.StartPoint);
            if (segment is PlanArc)
            {
                for (var i = 1; i < ArcSamples; i++)
                {
                    points.Add(segment.PointAt(segment.Length * i / ArcSamples));
                }
            }
        }

        return points;
    }

    private static PlanPoint Flip(PlanPoint point) => new PlanPoint(-point.X, point.Y);

    private static PlanPoint Move(PlanPoint point, double du, double dv) => new PlanPoint(point.X + du, point.Y + dv);
}
