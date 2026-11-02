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
}

/// <summary>Places section points on the path of an alignment.</summary>
internal static class SectionFrame
{
    /// <summary>
    /// The world position of a section point. The section stands in a vertical plane across the path, so the "up" of the
    /// section is always world up and only its direction across the road follows the path: u runs to the right of the
    /// direction of travel (<paramref name="tangentX"/>, <paramref name="tangentY"/> need not be a unit vector).
    /// </summary>
    /// <param name="verticalOffset">Added to every point's height, in feet.</param>
    public static PathPoint ToWorld(double u, double v, PathPoint centre, double tangentX, double tangentY, double verticalOffset)
    {
        var length = Math.Sqrt(tangentX * tangentX + tangentY * tangentY);
        if (length < 1e-12)
        {
            throw new ArgumentException("The path direction is undefined at this station.", nameof(tangentX));
        }

        var rightX = tangentY / length;
        var rightY = -tangentX / length;
        return new PathPoint(centre.X + u * rightX, centre.Y + u * rightY, centre.Z + v + verticalOffset);
    }
}
