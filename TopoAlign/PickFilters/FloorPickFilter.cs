using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Extensions;

namespace TopoAlign;

public class FloorPickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem.Category is not null && elem.Category.Id.IsCategory(BuiltInCategory.OST_Floors);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
