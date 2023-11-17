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

#if(REVIT2024_OR_GREATER)
        switch (elem.Category.Id.Value)
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
#else
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
#endif
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
