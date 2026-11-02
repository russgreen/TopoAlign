using Autodesk.Revit.DB;

namespace TopoAlign.Alignments;

/// <summary>Reads a Toposolid / TopographySurface into an elevation lookup.</summary>
internal static class TopographyReader
{
    /// <summary>
    /// Builds an elevation lookup from the upward-facing triangles of a Toposolid (2024+) or TopographySurface (earlier).
    /// </summary>
    public static TriangleSurface Read(Element topography)
    {
        if (topography is null)
        {
            throw new ArgumentNullException(nameof(topography));
        }

        var surface = new TriangleSurface();
        ForEachUpFacingTriangle(topography, (a, b, c) => surface.AddTriangle(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z));

        if (surface.TriangleCount == 0)
        {
            throw new InvalidOperationException("No surface geometry could be read from the selected toposolid.");
        }

        return surface;
    }

    /// <summary>
    /// Calls <paramref name="visit"/> with the corners (in model coordinates) of every triangle in the upward-facing
    /// faces of an element's geometry, looking inside family instances too.
    /// </summary>
    public static void ForEachUpFacingTriangle(Element element, Action<XYZ, XYZ, XYZ> visit)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        var options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false };
        var geometry = element.get_Geometry(options);
        if (geometry is not null)
        {
            AddGeometry(visit, geometry);
        }
    }

    private static void AddGeometry(Action<XYZ, XYZ, XYZ> visit, IEnumerable<GeometryObject> geometry)
    {
        foreach (var geometryObject in geometry)
        {
            switch (geometryObject)
            {
                case Solid solid:
                    foreach (Face face in solid.Faces)
                    {
                        if (FacesUp(face))
                        {
                            AddMesh(visit, face.Triangulate());
                        }
                    }

                    break;

                case Mesh mesh:
                    AddMesh(visit, mesh);
                    break;

                case GeometryInstance instance:
                    AddGeometry(visit, instance.GetInstanceGeometry());
                    break;
            }
        }
    }

    private static bool FacesUp(Face face)
    {
        if (face is PlanarFace planar)
        {
            return planar.FaceNormal.Z > 0.01;
        }

        var box = face.GetBoundingBox();
        var middle = new UV((box.Min.U + box.Max.U) / 2.0, (box.Min.V + box.Max.V) / 2.0);
        return face.ComputeNormal(middle).Z > 0.01;
    }

    private static void AddMesh(Action<XYZ, XYZ, XYZ> visit, Mesh mesh)
    {
        if (mesh is null)
        {
            return;
        }

        for (var i = 0; i < mesh.NumTriangles; i++)
        {
            var triangle = mesh.get_Triangle(i);
            visit(triangle.get_Vertex(0), triangle.get_Vertex(1), triangle.get_Vertex(2));
        }
    }
}
