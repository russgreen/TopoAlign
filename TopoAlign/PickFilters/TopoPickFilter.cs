using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign
{
    public class TopoPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category is null)
            {
                return false;
            }

            if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Topography)
            {
                return true;
            }

            return false;
            // Return (elem.Category.Id.IntegerValue.Equals(CInt(BuiltInCategory.OST_Topography)))
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}