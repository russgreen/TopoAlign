using Autodesk.Revit.DB;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>One continuous run of picked edges, sampled into points.</summary>
internal sealed class KerbChain
{
    public KerbChain(List<PathPoint> points, bool closed, double length, int curveCount)
    {
        Points = points;
        Closed = closed;
        Length = length;
        CurveCount = curveCount;
    }

    /// <summary>The path in order. For a closed loop the first point is not repeated at the end.</summary>
    public List<PathPoint> Points { get; }

    public bool Closed { get; }

    /// <summary>Length along the curves in feet.</summary>
    public double Length { get; }

    public int CurveCount { get; }
}
