using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TopoAlign.Sweep;
using TopoAlign.Sweep.Road;

namespace TopoAlign;

/// <summary>Accepts a road made by Sweep Along Alignment, as a direct shape or a family instance.</summary>
public class RoadPickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return RoadBuilder.IsRoad(elem);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
