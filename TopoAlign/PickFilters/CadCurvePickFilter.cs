using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign.PickFilters;

/// <summary>Lets the user pick curves on imported or linked CAD (DWG/DXF) instances.</summary>
internal sealed class CadCurvePickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem is ImportInstance;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return true;
    }
}
