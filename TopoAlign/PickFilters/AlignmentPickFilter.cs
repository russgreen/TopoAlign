using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TopoAlign.Alignments;

namespace TopoAlign;

public class AlignmentPickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        if (elem is not DirectShape)
        {
            return false;
        }

        return AlignmentApi.IsValid(elem);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
