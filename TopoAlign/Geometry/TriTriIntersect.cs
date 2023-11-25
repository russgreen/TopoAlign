using Autodesk.Revit.DB;
using g3;

namespace TopoAlign.Geometry;

public class TriTriIntersect
{

#if REVIT2024_OR_GREATER
    public static List<Triangle3d> TrianglesFromTopo(Autodesk.Revit.DB.Toposolid topoSolid)
    {
        Options op = new Options
        {
            ComputeReferences = true
        };
        var geoObjects = topoSolid.get_Geometry(op).GetEnumerator();

        List<Triangle3d> topoTriangles = new List<Triangle3d>();

        while (geoObjects.MoveNext())
        {
           Solid geoObj = geoObjects.Current as Solid;

            foreach (Face face in geoObj.Faces)
            {
                topoTriangles.AddRange(TrianglesFromGeoObj(face));
            }
        }

        return topoTriangles;
    }
#else
    public static List<Triangle3d> TrianglesFromTopo(Autodesk.Revit.DB.Architecture.TopographySurface topoSurface)
    {
        Options op = new()
        {
            ComputeReferences = true
        };
        var geoObjects = topoSurface.get_Geometry(op).GetEnumerator();

        List<Triangle3d> topoTriangles = new();

        while (geoObjects.MoveNext())
        {
            GeometryObject geoObj = geoObjects.Current;

            if (geoObj is Mesh)
            {
                Mesh mesh = geoObj as Mesh;

                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    var meshTriangle = mesh.get_Triangle(i);
                    XYZ T11 = meshTriangle.get_Vertex(0);
                    XYZ T12 = meshTriangle.get_Vertex(1);
                    XYZ T13 = meshTriangle.get_Vertex(2);

                    Vector3d v1 = new(T11.X, T11.Y, T11.Z);
                    Vector3d v2 = new(T12.X, T12.Y, T12.Z);
                    Vector3d v3 = new(T13.X, T13.Y, T13.Z);

                    Triangle3d triangle3D = new(v1, v2, v3);


                    topoTriangles.Add(triangle3D);
                }
            }
        }

        return topoTriangles;
    }
#endif

    public static List<Triangle3d> TrianglesFromGeoObj(Face face)
    {
        List<Triangle3d> faceTriangles = new();
        Mesh mesh = face.Triangulate();

        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            var meshTriangle = mesh.get_Triangle(i);
            XYZ T11 = meshTriangle.get_Vertex(0);
            XYZ T12 = meshTriangle.get_Vertex(1);
            XYZ T13 = meshTriangle.get_Vertex(2);

            Vector3d v1 = new(T11.X, T11.Y, T11.Z);
            Vector3d v2 = new(T12.X, T12.Y, T12.Z);
            Vector3d v3 = new(T13.X, T13.Y, T13.Z);

            Triangle3d triangle3D = new(v1, v2, v3);

            faceTriangles.Add(triangle3D);
        }

        if (faceTriangles.Count == 2)
        {
            // we have a flat face and should try to sub-divide
            var vertices = new List<Vector3f>();
            foreach (var p in mesh.Vertices)
            {
                vertices.Add((Vector3f)new Vector3d(p.X, p.Y, p.Z));
            }

            Vector3d[] normals = new Vector3d[mesh.Vertices.Count];
            Index3i[] triangles = new Index3i[faceTriangles.Count];
            int tid = 0;
#if REVIT2019
            int nid = 0;
#endif
            foreach (var t in faceTriangles)
            {
                //get the vertices from the triangle
#if REVIT2019
                normals.SetValue(t.Normal, nid);
                nid = IncrementInt(nid, mesh.Vertices.Count);
                normals.SetValue(t.Normal, nid);
                nid = IncrementInt(nid, mesh.Vertices.Count);
                normals.SetValue(t.Normal, nid);
                nid = IncrementInt(nid, mesh.Vertices.Count);

#else
                normals.Append(t.Normal);
                normals.Append(t.Normal);
                normals.Append(t.Normal);
#endif


                //lookup the vid from V1 for each vertex in t
                int vID0 = vertices.IndexOf((Vector3f)t.V0);
                int vID1 = vertices.IndexOf((Vector3f)t.V1);
                int vID2 = vertices.IndexOf((Vector3f)t.V2);

                triangles[tid] = new Index3i(vID0, vID1, vID2);
                tid++;
            }

            DMesh3 dMesh = DMesh3Builder.Build(vertices, triangles, normals);

            Remesher r = new(dMesh);
            r.SetTargetEdgeLength(5);
            for (int i = 0; i < 10; i++)
            {
                r.BasicRemeshPass();
            }

            var isValid = dMesh.CheckValidity();

            var meshTriangles = dMesh.Triangles();

            faceTriangles.Clear();

            foreach (var t in meshTriangles)
            {
                Vector3d v1 = dMesh.GetVertex(t.a);
                Vector3d v2 = dMesh.GetVertex(t.b);
                Vector3d v3 = dMesh.GetVertex(t.c);

                Triangle3d triangle3D = new(v1, v2, v3);

                faceTriangles.Add(triangle3D);
            }
        }

        return faceTriangles;
    }

    public static List<Vector3d> IntersectTriangleLists(List<Triangle3d> topoTriangles, List<Triangle3d> faceTriangles, double divide = 1000d * 0.00328084d)
    {
        var points = new List<Vector3d>();

        foreach (var topoTriangle in topoTriangles)
        {
            foreach (var faceTriangle in faceTriangles)
            {
                IntrTriangle3Triangle3 tritri = new(topoTriangle, faceTriangle);

                if (tritri.Test())
                {
                    tritri.Find();

                    if (tritri.Result == g3.IntersectionResult.Intersects)
                    {
                        points.Add(tritri.Points.V0);

                        if (tritri.Type == g3.IntersectionType.Segment)
                        {
                            points.Add(tritri.Points.V1);

                            var deltaX = tritri.Points.V1.x - tritri.Points.V0.x;
                            var deltaY = tritri.Points.V1.y - tritri.Points.V0.y;
                            var deltaZ = tritri.Points.V1.z - tritri.Points.V0.z;

                            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

                            if (distance > divide)
                            {
                                //TODO - create intermeditate points
                            }
                        }
                    }
                }
            }
        }

        if (points.Count == 0)
        {
            points = null;
        }

        return points;
    }


#if REVIT2019
    private static int IncrementInt(int val, int max)
    {
        if(val < max)
        {
            return val++;
        }
        else
        {
            return val;
        }
    }
#endif
}
