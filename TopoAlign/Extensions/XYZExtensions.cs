using Autodesk.Revit.DB;

namespace TopoAlign.Extensions;

internal static class XYZExtensions
{
    /// <summary>
    /// This method is used to visualize XYZ in a document
    /// </summary>
    /// <param name="point"></param>
    /// <param name="document"></param>
    public static void Visualize(
        this XYZ point, Document document)
    {
        document.CreateDirectShape(Autodesk.Revit.DB.Point.Create(point));
    }


}
