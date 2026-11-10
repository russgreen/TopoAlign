namespace TopoAlign.Grading;

/// <summary>New points sorted by how safe they are to put into a toposolid, given where its outline is.</summary>
internal sealed class BoundarySplit
{
    /// <summary>Well inside the outline: added in one go.</summary>
    public List<GradePoint> Add { get; } = new();

    /// <summary>On an outline edge between its corners. Revit may refuse these, so they are tried one at a time.</summary>
    public List<GradePoint> OnEdge { get; } = new();

    /// <summary>At an outline corner. A toposolid can't hold two points in one place, so the corner is moved to this height instead.</summary>
    public List<GradePoint> MoveCorner { get; } = new();

    /// <summary>Outside the outline, so Revit can't take them.</summary>
    public List<GradePoint> Outside { get; } = new();
}
