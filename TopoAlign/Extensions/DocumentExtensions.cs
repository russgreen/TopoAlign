using Autodesk.Revit.DB;

namespace TopoAlign.Extensions;

internal static class DocumentExtensions
{
    /// <summary>
    /// This method is used to create direct shapes in a Revit Document
    /// </summary>
    /// <param name="document"></param>
    /// <param name="geometryObjects"></param>
    /// <param name="builtInCategory"></param>
    /// <returns></returns>
    public static DirectShape CreateDirectShape(
        this Document document,
        IEnumerable<GeometryObject> geometryObjects,
        BuiltInCategory builtInCategory = BuiltInCategory.OST_GenericModel)
    {

        var directShape = DirectShape.CreateElement(document, new ElementId(builtInCategory));
        directShape.SetShape(geometryObjects.ToList());
        return directShape;

    }

    public static DirectShape CreateDirectShape(
        this Document document,
        GeometryObject geometryObject,
        BuiltInCategory builtInCategory = BuiltInCategory.OST_GenericModel)
    {

        var directShape = DirectShape.CreateElement(document, new ElementId(builtInCategory));
        directShape.SetShape(new List<GeometryObject>() { geometryObject });
        return directShape;
    }
}
