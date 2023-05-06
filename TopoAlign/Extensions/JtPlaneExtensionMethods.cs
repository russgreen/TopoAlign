using Autodesk.Revit.DB;
using System.Diagnostics;
using TopoAlign.Geometry;

namespace TopoAlign.Extensions;
internal static class JtPlaneExtensionMethods
{

        /// <summary>
        /// Return the signed distance from 
        /// a plane to a given point.
        /// </summary>
        public static double SignedDistanceTo(
          this Plane plane,
          XYZ p)
        {
            Debug.Assert(
GeometryComparison.IsEqual(plane.Normal.GetLength(), 1),
              "expected normalised plane normal");

            XYZ v = p - plane.Origin;

            return plane.Normal.DotProduct(v);
        }

        /// <summary>
        /// Project given 3D XYZ point onto plane.
        /// </summary>
        public static XYZ ProjectOnto(
          this Plane plane,
          XYZ p)
        {
            double d = plane.SignedDistanceTo(p);

            //XYZ q = p + d * plane.Normal; // wrong according to Ruslan Hanza and Alexander Pekshev in their comments http://thebuildingcoder.typepad.com/blog/2014/09/planes-projections-and-picking-points.html#comment-3765750464
            XYZ q = p - d * plane.Normal;

            Debug.Assert(
GeometryComparison.IsZero(plane.SignedDistanceTo(q)),
              "expected point on plane to have zero distance to plane");

            return q;
        }

        /// <summary>
        /// Project given 3D XYZ point into plane, 
        /// returning the UV coordinates of the result 
        /// in the local 2D plane coordinate system.
        /// </summary>
        public static UV ProjectInto(
          this Plane plane,
          XYZ p)
        {
            XYZ q = plane.ProjectOnto(p);
            XYZ o = plane.Origin;
            XYZ d = q - o;
            double u = d.DotProduct(plane.XVec);
            double v = d.DotProduct(plane.YVec);
            return new UV(u, v);
        }
 
}
