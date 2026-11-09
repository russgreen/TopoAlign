using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>What the road is made into in the model.</summary>
internal enum RoadOutput
{
    DirectShape,
    Family,

    /// <summary>No road is added; the swept geometry is only used to make a toposolid (Revit 2024 and later).</summary>
    Toposolid
}
