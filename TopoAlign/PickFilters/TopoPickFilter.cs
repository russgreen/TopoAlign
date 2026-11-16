using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Extensions;

namespace TopoAlign;

public class TopoPickFilter : ISelectionFilter
{
#if REVIT2024_OR_GREATER
    private const BuiltInCategory TopoCategory = BuiltInCategory.OST_Toposolid;
#else
    private const BuiltInCategory TopoCategory = BuiltInCategory.OST_Topography;
#endif

    public bool AllowElement(Element elem)
    {
        return elem.Category is not null && elem.Category.Id.IsCategory(TopoCategory);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
