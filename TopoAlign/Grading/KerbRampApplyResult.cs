#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

internal sealed class KerbRampApplyResult
{
    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }
    public double RampLength { get; set; }
    public double StartFlareLength { get; set; }
    public double EndFlareLength { get; set; }
    public List<string> Warnings { get; } = new();
}
#endif
