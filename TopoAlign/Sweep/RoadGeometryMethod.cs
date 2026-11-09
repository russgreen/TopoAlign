using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>How the road solid is made.</summary>
internal enum RoadGeometryMethod
{
    /// <summary>A separate loft between every pair of sections: robust, but every section leaves a seam.</summary>
    SegmentedLoft,

    /// <summary>Smooth surfaces running the length of the road, so there are no seams between sections.</summary>
    SmoothSurfaces
}
