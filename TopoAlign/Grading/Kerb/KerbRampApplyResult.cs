#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading.Kerb;

internal sealed class KerbRampApplyResult
{
    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }

    /// <summary>Solids cut in a road part's family; zero when the footway is a toposolid.</summary>
    public int SolidsCut { get; set; }

    /// <summary>The family that was cut, or null when the footway is a toposolid.</summary>
    public string? FamilyName { get; set; }

    public double RampLength { get; set; }
    public double StartFlareLength { get; set; }
    public double EndFlareLength { get; set; }
    public List<string> Warnings { get; } = new();
}
#endif
