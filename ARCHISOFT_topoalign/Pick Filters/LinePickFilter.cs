using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace ARCHISOFT_topoalign
{
    public class LinePickFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Convert the element to a ModelLine
            ModelLine line = elem as ModelLine;
            ModelCurve curve = elem as ModelCurve;
            // line is null if the element is not a model line
            if (line is null & curve is null)
            {
                return false;
            }
            // return true if the line is a model line
            return true;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}