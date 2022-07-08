using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign;

public class FloorPickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem.Category is null)
        {
            return false;
        }

        switch (elem.Category.Id.IntegerValue)
        {
            case (int)BuiltInCategory.OST_Floors:
            {
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
