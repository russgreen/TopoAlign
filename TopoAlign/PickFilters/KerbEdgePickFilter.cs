using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TopoAlign.Sweep.Road;

namespace TopoAlign;

/// <summary>Lets an edge be picked on a toposolid, or on a road part that is still a family (a road not converted to a toposolid).</summary>
public class KerbEdgePickFilter : ISelectionFilter
{
    private readonly TopoPickFilter _topo = new();

    public bool AllowElement(Element elem) =>
        _topo.AllowElement(elem) || (elem is FamilyInstance instance && RoadFamilyBuilder.IsRoadFamily(instance.Symbol?.Family));

    public bool AllowReference(Reference reference, XYZ position) => true;
}
