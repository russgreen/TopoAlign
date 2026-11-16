using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Extensions;

namespace TopoAlign;

public class ElemPickFilter : ISelectionFilter
{
    private static readonly BuiltInCategory[] Categories =
    {
        BuiltInCategory.OST_Topography,
        BuiltInCategory.OST_BuildingPad,
        BuiltInCategory.OST_Floors,
        BuiltInCategory.OST_Roofs,
        BuiltInCategory.OST_Site,
#if REVIT2024_OR_GREATER
        BuiltInCategory.OST_Roads,
        BuiltInCategory.OST_Hardscape,
#endif
        BuiltInCategory.OST_GenericModel,
        BuiltInCategory.OST_Furniture,
        BuiltInCategory.OST_Mass,
        BuiltInCategory.OST_Planting,
        BuiltInCategory.OST_Walls
    };

    public bool AllowElement(Element elem)
    {
        if (elem.Category is null)
        {
            return false;
        }

        foreach (var category in Categories)
        {
            if (elem.Category.Id.IsCategory(category))
            {
                return true;
            }
        }

        return false;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
