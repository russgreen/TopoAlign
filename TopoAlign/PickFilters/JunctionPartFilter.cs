using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Civil;

namespace TopoAlign;

/// <summary>Accepts a T-junction surface, or an alignment that a T-junction was made from.</summary>
public class JunctionPartFilter : ISelectionFilter
{
    /// <summary>True for a junction surface made by <see cref="TJunctionSurfaceBuilder"/>, or for an alignment.</summary>
    public static bool IsJunctionPart(Element? element)
    {
        return element switch
        {
            DirectShape shape => shape.ApplicationId == TJunctionSurfaceBuilder.ApplicationId,
            Element other => AlignmentApi.IsValid(other),
            _ => false
        };
    }

    public bool AllowElement(Element elem)
    {
        return IsJunctionPart(elem);
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}
