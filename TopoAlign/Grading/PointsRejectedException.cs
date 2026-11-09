#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>Revit would not build a shape from the points, and the toposolid was left as it was.</summary>
internal sealed class PointsRejectedException : InvalidOperationException
{
    public PointsRejectedException(string message)
        : base(message)
    {
    }
}
#endif
