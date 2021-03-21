using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign
{
    public class ElemPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category is null)
            {
                return false;
            }

            switch (elem.Category.Id.IntegerValue)
            {
                case (int)BuiltInCategory.OST_Topography:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_BuildingPad:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Floors:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Roofs:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Site:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_GenericModel:
                {
                    return true;
                }
                // Case CInt(BuiltInCategory.OST_Stairs) : Return True
                // Case CInt(BuiltInCategory.OST_Ramps) : Return True
                case (int)BuiltInCategory.OST_Furniture:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Mass:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Planting:
                {
                    return true;
                }

                case (int)BuiltInCategory.OST_Walls:
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
}