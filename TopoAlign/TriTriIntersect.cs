using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using g3;

namespace TopoAlign
{
    public class TriTriIntersect
    {

        public static List<Triangle3d> TrianglesFromTopo(Autodesk.Revit.DB.Architecture.TopographySurface topoSurface)
        {
            Options op = new Options
            {
                ComputeReferences = true
            };
            var geoObjects = topoSurface.get_Geometry(op).GetEnumerator();

            List<Triangle3d> topoTriangles = new List<Triangle3d>();

            while (geoObjects.MoveNext())
            {
                GeometryObject geoObj = geoObjects.Current as GeometryObject;

                if (geoObj is Mesh)
                {
                    Mesh mesh = geoObj as Mesh;

                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        var meshTriangle = mesh.get_Triangle(i);
                        XYZ T11 = meshTriangle.get_Vertex(0);
                        XYZ T12 = meshTriangle.get_Vertex(1);
                        XYZ T13 = meshTriangle.get_Vertex(2);

                        Vector3d v1 = new Vector3d(T11.X, T11.Y, T11.Z);
                        Vector3d v2 = new Vector3d(T12.X, T12.Y, T12.Z);
                        Vector3d v3 = new Vector3d(T13.X, T13.Y, T13.Z);
                        
                        Triangle3d triangle3D = new Triangle3d(v1, v2, v3);


                        topoTriangles.Add(triangle3D);
                    }
                }
            }

            return topoTriangles;
        }

        public static List<Triangle3d> TrianglesFromGeoObj(Face face)
        {
            List<Triangle3d> faceTriangles = new List<Triangle3d>();

            Mesh mesh = face.Triangulate();

            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                var meshTriangle = mesh.get_Triangle(i);
                XYZ T11 = meshTriangle.get_Vertex(0);
                XYZ T12 = meshTriangle.get_Vertex(1);
                XYZ T13 = meshTriangle.get_Vertex(2);

                Vector3d v1 = new Vector3d(T11.X, T11.Y, T11.Z);
                Vector3d v2 = new Vector3d(T12.X, T12.Y, T12.Z);
                Vector3d v3 = new Vector3d(T13.X, T13.Y, T13.Z);

                Triangle3d triangle3D = new Triangle3d(v1, v2, v3);

                faceTriangles.Add(triangle3D);
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
                    IntrTriangle3Triangle3 tritri = new IntrTriangle3Triangle3(topoTriangle, faceTriangle);

                    if (tritri.Test() == true)
                    {
                        tritri.Find();

                        if(tritri.Result == g3.IntersectionResult.Intersects)
                        {
                            points.Add(tritri.Points.V0);

                            if(tritri.Type == IntersectionType.Segment)
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

            if(points.Count == 0)
            {
                points = null;
            }
                        
            return points;
        }
    }
}
