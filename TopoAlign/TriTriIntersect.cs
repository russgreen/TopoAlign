using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using GeometRi;

namespace TopoAlign
{
    public class TriTriIntersect
    {

        public static List<Triangle> TrianglesFromTopo(Autodesk.Revit.DB.Architecture.TopographySurface topoSurface)
        {
            Options op = new Options
            {
                ComputeReferences = true
            };
            var geoObjects = topoSurface.get_Geometry(op).GetEnumerator();

            List<Triangle> topoTriangles = new List<Triangle>();

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

                        Point3d T21 = new Point3d(T11.X, T11.Y, T11.Z);
                        Point3d T22 = new Point3d(T12.X, T12.Y, T12.Z);
                        Point3d T23 = new Point3d(T13.X, T13.Y, T13.Z);

                        Triangle triangle = new Triangle(T21, T22, T23);

                        topoTriangles.Add(triangle);
                    }
                }
            }

            return topoTriangles;
        }

        public static List<Triangle> TrianglesFromGeoObj(Face face)
        {
            List<Triangle> faceTriangles = new List<Triangle>();

            Mesh mesh = face.Triangulate();

            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                var meshTriangle = mesh.get_Triangle(i);
                XYZ T11 = meshTriangle.get_Vertex(0);
                XYZ T12 = meshTriangle.get_Vertex(1);
                XYZ T13 = meshTriangle.get_Vertex(2);

                Point3d T21 = new Point3d(T11.X, T11.Y, T11.Z);
                Point3d T22 = new Point3d(T12.X, T12.Y, T12.Z);
                Point3d T23 = new Point3d(T13.X, T13.Y, T13.Z);

                Triangle triangle = new Triangle(T21, T22, T23);

                faceTriangles.Add(triangle);
            }

            return faceTriangles;
        }

        public static List<Segment3d> IntersectTriangleLists(List<Triangle> topoTriangles, List<Triangle> faceTriangles)
        {
            List<Segment3d> segments = new List<Segment3d>();

            foreach (var topoTriangle in topoTriangles)
            {
                foreach (var faceTriangle in faceTriangles)
                {
                    var segment = IntersectTriangles(topoTriangle, faceTriangle);
                    if(segment != null)
                    {
                        segments.Add(segment);
                    }
                }
            }

            if(segments.Count == 0)
            {
                segments = null;
            }
                        
            return segments;

        }

        private static Segment3d IntersectTriangles(Triangle t1, Triangle t2)
        {
            //Segment3d segment3D = new Segment3d(new Point3d(0,0,0), new Point3d(0,0,0));
            Segment3d segment3D;

            if (t1.Intersects(t2) == true)
            {
                Debug.WriteLine("Intersection found");

                object obj = t1.IntersectionWith(t2.ToPlane);

                if (obj != null)
                {
                    if (obj.GetType() == typeof(Segment3d))
                    {
                        Debug.WriteLine("Intersection is segment");
                        segment3D = (Segment3d)obj;
                        Debug.WriteLine(segment3D.ToString());

                        return segment3D;
                    }                    
                }                
            }

            segment3D = null;
            return segment3D;
        }
    }
}
