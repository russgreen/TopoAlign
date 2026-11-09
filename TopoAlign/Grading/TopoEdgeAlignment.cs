namespace TopoAlign.Grading;

/// <summary>The points that make two neighbouring topographies meet along the edge they share.</summary>
internal sealed class TopoEdgeAlignment
{
    /// <summary>Points to add to the first topography, so its edge passes through the second one's edge points.</summary>
    public List<GradePoint> AddToFirst { get; } = new();

    /// <summary>Points to add to the second topography, so its edge passes through the first one's edge points.</summary>
    public List<GradePoint> AddToSecond { get; } = new();

    /// <summary>
    /// Points the first topography already has, at the place and the height they should have to meet the second topography's edge.
    /// A topography can't hold two points in the same place, so these are moved rather than added. The second topography is the reference and isn't moved.
    /// </summary>
    public List<GradePoint> ChangeOnFirst { get; } = new();

    public int SharedPoints { get; set; }
}
