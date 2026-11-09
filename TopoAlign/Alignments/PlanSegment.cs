namespace TopoAlign.Alignments;

/// <summary>
/// One piece of a horizontal alignment in plan: a straight, a circular arc or a clothoid spiral.
/// Pure geometry with no Revit types, so alignments can be laid out and tested without a model.
/// </summary>
internal abstract class PlanSegment
{
    public abstract double Length { get; }
    public abstract PlanPoint StartPoint { get; }
    public abstract PlanPoint EndPoint { get; }

    /// <summary>Point at a distance along the segment, 0 to <see cref="Length"/>.</summary>
    public abstract PlanPoint PointAt(double distance);

    /// <summary>Direction of travel, in radians from +X, at a distance along the segment.</summary>
    public abstract double HeadingAt(double distance);

    /// <summary>The same segment traversed the other way.</summary>
    public abstract PlanSegment Reversed();

    /// <summary>Reverses a whole chain: the order of the segments and the direction of each.</summary>
    public static List<PlanSegment> ReverseChain(IReadOnlyList<PlanSegment> segments)
    {
        var result = new List<PlanSegment>(segments.Count);
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            result.Add(segments[i].Reversed());
        }

        return result;
    }

    /// <summary>Total length of a chain.</summary>
    public static double TotalLength(IEnumerable<PlanSegment> segments)
    {
        var total = 0.0;
        foreach (var segment in segments)
        {
            total += segment.Length;
        }

        return total;
    }
}
