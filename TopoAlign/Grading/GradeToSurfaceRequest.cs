#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>What the dialog hands back to the command.</summary>
internal sealed class GradeToSurfaceRequest
{
    public GradeToSurfaceRequest(
        Toposolid toposolid,
        IReadOnlyList<Curve> curves,
        XYZ sidePoint,
        SlopeMode slopeMode,
        double slopeValue,
        double maxWidth,
        double verticalOffset,
        double interval,
        bool addEdgePoints)
    {
        Toposolid = toposolid;
        Curves = curves;
        SidePoint = sidePoint;
        SlopeMode = slopeMode;
        SlopeValue = slopeValue;
        MaxWidth = maxWidth;
        VerticalOffset = verticalOffset;
        Interval = interval;
        AddEdgePoints = addEdgePoints;
    }

    public Toposolid Toposolid { get; }

    /// <summary>The line to grade from, in order and end to end.</summary>
    public IReadOnlyList<Curve> Curves { get; }

    public XYZ SidePoint { get; }
    public SlopeMode SlopeMode { get; }
    public double SlopeValue { get; }

    /// <summary>All lengths are in feet.</summary>
    public double MaxWidth { get; }

    public double VerticalOffset { get; }
    public double Interval { get; }
    public bool AddEdgePoints { get; }
}
#endif
