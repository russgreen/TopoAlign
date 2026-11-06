using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign;

/// <summary>Lets an edge be picked, but only on a toposolid (or topography surface in older Revit versions).</summary>
public class TopoEdgePickFilter : ISelectionFilter
{
    private readonly TopoPickFilter _topo = new();

    public bool AllowElement(Element elem) => _topo.AllowElement(elem);

    public bool AllowReference(Reference reference, XYZ position) => true;
}
