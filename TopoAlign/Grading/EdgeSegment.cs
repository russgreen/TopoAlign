namespace TopoAlign.Grading;

/// <summary>One piece of a topography's outline, in feet.</summary>
internal readonly struct EdgeSegment
{
    public EdgeSegment(GradePoint start, GradePoint end)
    {
        Start = start;
        End = end;
    }

    public GradePoint Start { get; }
    public GradePoint End { get; }
}
