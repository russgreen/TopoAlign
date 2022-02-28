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

#if REVIT2018 || REVIT2019 || REVIT2020 || REVIT2021 || REVIT2022
#else
            //if (elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Toposolid)
            //{
            //    return true;
            //}
#endif
            return false;
            // Return (elem.Category.Id.IntegerValue.Equals(CInt(BuiltInCategory.OST_Topography)))
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}