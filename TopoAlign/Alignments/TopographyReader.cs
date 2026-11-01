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
        var options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false };

        var geometry = topography.get_Geometry(options);
        if (geometry is not null)
        {
            AddGeometry(surface, geometry);
        }

        if (surface.TriangleCount == 0)
        {
            throw new InvalidOperationException("No surface geometry could be read from the selected toposolid.");
        }

        return surface;
    }

    private static void AddGeometry(TriangleSurface surface, IEnumerable<GeometryObject> geometry)
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
                            AddMesh(surface, face.Triangulate());
                        }
                    }

                    break;

                case Mesh mesh:
                    AddMesh(surface, mesh);
                    break;

                case GeometryInstance instance:
                    AddGeometry(surface, instance.GetInstanceGeometry());
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

    private static void AddMesh(TriangleSurface surface, Mesh mesh)
    {
        if (mesh is null)
        {
            return;
        }

        for (var i = 0; i < mesh.NumTriangles; i++)
        {
            var triangle = mesh.get_Triangle(i);
            var a = triangle.get_Vertex(0);
            var b = triangle.get_Vertex(1);
            var c = triangle.get_Vertex(2);
            surface.AddTriangle(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z);
        }
    }
}
