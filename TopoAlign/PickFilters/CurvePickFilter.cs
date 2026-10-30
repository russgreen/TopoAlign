using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign;

public class CurvePickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem is not ModelCurve and not DetailCurve)
        {
            return false;
        }

        if (elem is not CurveElement curveElement)
        {
            return false;
        }

        return curveElement.GeometryCurve is Line or Arc;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
