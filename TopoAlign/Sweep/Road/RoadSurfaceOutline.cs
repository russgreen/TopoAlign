namespace TopoAlign.Sweep.Road;

/// <summary>The plan outline and surface points of a road's top surface; what a toposolid is made from.</summary>
internal sealed class RoadSurfaceOutline
{
    public RoadSurfaceOutline(List<SurfaceVertex> points, List<List<SurfaceVertex>> loops, int openChains)
    {
        Points = points;
        Loops = loops;
        OpenChains = openChains;
    }

    /// <summary>One point for each place on the surface (at most one per XY position, the highest if the surface overhangs).</summary>
    public IReadOnlyList<SurfaceVertex> Points { get; }

    /// <summary>The closed outlines of the surface, one per separate piece (a gap in the road gives two). Collinear points are removed.</summary>
    public IReadOnlyList<IReadOnlyList<SurfaceVertex>> Loops { get; }

    /// <summary>Edges that could not be closed into an outline and were left out; non-zero means the surface has a crack.</summary>
    public int OpenChains { get; }
}
