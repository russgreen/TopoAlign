﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using WinForms = System.Windows.Forms;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections;

namespace TopoAlign
{
    public class Util
    {

        #region Geometrical Comparison
        public const double _eps = 1.0e-9;

        public static double Eps
        {
            get
            {
                return _eps;
            }
        }

        public static double MinLineLength
        {
            get
            {
                return _eps;
            }
        }

        public static double TolPointOnPlane
        {
            get
            {
                return _eps;
            }
        }

        public static bool IsZero(
          double a,
          double tolerance = _eps)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsEqual(
          double a,
          double b,
          double tolerance = _eps)
        {
            return IsZero(b - a, tolerance);
        }

        public static bool IsLessOrEqual(
          double a,
          double b,
          double tolerance = _eps)
        {
            return IsZero(b - a, tolerance);
        }

        public static int Compare(
          double a,
          double b,
          double tolerance = _eps)
        {
            return IsEqual(a, b, tolerance)
              ? 0
              : (a < b ? -1 : 1);
        }

        public static int Compare(
          XYZ p,
          XYZ q,
          double tolerance = _eps)
        {
            int d = Compare(p.X, q.X, tolerance);

            if (0 == d)
            {
                d = Compare(p.Y, q.Y, tolerance);

                if (0 == d)
                {
                    d = Compare(p.Z, q.Z, tolerance);
                }
            }
            return d;
        }

        /// <summary>
        /// Implement a comparison operator for lines 
        /// in the XY plane useful for sorting into 
        /// groups of parallel lines.
        /// </summary>
        public static int Compare(Line a, Line b)
        {
            XYZ pa = a.GetEndPoint(0);
            XYZ qa = a.GetEndPoint(1);
            XYZ pb = b.GetEndPoint(0);
            XYZ qb = b.GetEndPoint(1);
            XYZ va = qa - pa;
            XYZ vb = qb - pb;

            // Compare angle in the XY plane

            double ang_a = Math.Atan2(va.Y, va.X);
            double ang_b = Math.Atan2(vb.Y, vb.X);

            int d = Compare(ang_a, ang_b);

            if (0 == d)
            {
                // Compare distance of unbounded line to origin

                double da = (qa.X * pa.Y - qa.Y * pa.Y)
                  / va.GetLength();

                double db = (qb.X * pb.Y - qb.Y * pb.Y)
                  / vb.GetLength();

                d = Compare(da, db);

                if (0 == d)
                {
                    // Compare distance of start point to origin

                    d = Compare(pa.GetLength(), pb.GetLength());

                    if (0 == d)
                    {
                        // Compare distance of end point to origin

                        d = Compare(qa.GetLength(), qb.GetLength());
                    }
                }
            }
            return d;
        }

        public static int Compare(Plane a, Plane b)
        {
            int d = Compare(a.Normal, b.Normal);

            if (0 == d)
            {
                d = Compare(a.SignedDistanceTo(XYZ.Zero),
                  b.SignedDistanceTo(XYZ.Zero));

                if (0 == d)
                {
                    d = Compare(a.XVec.AngleOnPlaneTo(
                      b.XVec, b.Normal), 0);
                }
            }
            return d;
        }

        /// <summary>
        /// Predicate to test whewther two points or 
        /// vectors can be considered equal with the 
        /// given tolerance.
        /// </summary>
        public static bool IsEqual(
          XYZ p,
          XYZ q,
          double tolerance = _eps)
        {
            return 0 == Compare(p, q, tolerance);
        }

        /// <summary>
        /// Return true if the given bounding box bb
        /// contains the given point p in its interior.
        /// </summary>
        public bool BoundingBoxXyzContains(
          BoundingBoxXYZ bb,
          XYZ p)
        {
            return 0 < Compare(bb.Min, p)
              && 0 < Compare(p, bb.Max);
        }

        /// <summary>
        /// Return true if the vectors v and w 
        /// are non-zero and perpendicular.
        /// </summary>
        bool IsPerpendicular(XYZ v, XYZ w)
        {
            double a = v.GetLength();
            double b = v.GetLength();
            double c = Math.Abs(v.DotProduct(w));
            return _eps < a
              && _eps < b
              && _eps > c;
            // c * c < _eps * a * b
        }

        public static bool IsParallel(XYZ p, XYZ q)
        {
            return p.CrossProduct(q).IsZeroLength();
        }

        public static bool IsCollinear(Line a, Line b)
        {
            XYZ v = a.Direction;
            XYZ w = b.Origin - a.Origin;
            return IsParallel(v, b.Direction)
              && IsParallel(v, w);
        }

        public static bool IsHorizontal(XYZ v)
        {
            return IsZero(v.Z);
        }

        public static bool IsVertical(XYZ v)
        {
            return IsZero(v.X) && IsZero(v.Y);
        }

        public static bool IsVertical(XYZ v, double tolerance)
        {
            return IsZero(v.X, tolerance)
              && IsZero(v.Y, tolerance);
        }

        public static bool IsHorizontal(Edge e)
        {
            XYZ p = e.Evaluate(0);
            XYZ q = e.Evaluate(1);
            return IsHorizontal(q - p);
        }

        public static bool IsHorizontal(PlanarFace f)
        {
            return IsVertical(f.FaceNormal);
        }

        public static bool IsVertical(PlanarFace f)
        {
            return IsHorizontal(f.FaceNormal);
        }

        public static bool IsVertical(CylindricalFace f)
        {
            return IsVertical(f.Axis);
        }

        /// <summary>
        /// Minimum slope for a vector to be considered
        /// to be pointing upwards. Slope is simply the
        /// relationship between the vertical and
        /// horizontal components.
        /// </summary>
        const double _minimumSlope = 0.3;

        /// <summary>
        /// Return true if the Z coordinate of the
        /// given vector is positive and the slope
        /// is larger than the minimum limit.
        /// </summary>
        public static bool PointsUpwards(XYZ v)
        {
            double horizontalLength = v.X * v.X + v.Y * v.Y;
            double verticalLength = v.Z * v.Z;

            return 0 < v.Z && _minimumSlope < verticalLength / horizontalLength;
        }

        public static bool PointsDownwards(XYZ v)
        {
            double horizontalLength = v.X * v.X + v.Y * v.Y;
            double verticalLength = v.Z * v.Z;
            return 0d > v.Z && _minimumSlope < verticalLength / horizontalLength;
        }

        /// <summary>
        /// Return the maximum value from an array of real numbers.
        /// </summary>
        public static double Max(double[] a)
        {
            Debug.Assert(1 == a.Rank, "expected one-dimensional array");
            Debug.Assert(0 == a.GetLowerBound(0), "expected zero-based array");
            Debug.Assert(0 < a.GetUpperBound(0), "expected non-empty array");
            double max = a[0];
            for (int i = 1; i <= a.GetUpperBound(0); ++i)
            {
                if (max < a[i])
                {
                    max = a[i];
                }
            }
            return max;
        }
        #endregion // Geometrical Comparison

        #region Flatten, i.e. project from 3D to 2D by dropping the Z coordinate
        /// <summary>
        /// Eliminate the Z coordinate.
        /// </summary>
        public static UV Flatten(XYZ point)
        {
            return new UV(point.X, point.Y);
        }

        /// <summary>
        /// Eliminate the Z coordinate.
        /// </summary>
        public static List<UV> Flatten(List<XYZ> polygon)
        {
            double z = polygon[0].Z;
            var a = new List<UV>(polygon.Count);
            foreach (XYZ p in polygon)
            {
                Debug.Assert(IsEqual(p.Z, z), "expected horizontal polygon");
                a.Add(Flatten(p));
            }

            return a;
        }

        /// <summary>
        /// Eliminate the Z coordinate.
        /// </summary>
        public static List<List<UV>> Flatten(List<List<XYZ>> polygons)
        {
            double z = polygons[0][0].Z;
            var a = new List<List<UV>>(polygons.Count);
            foreach (List<XYZ> polygon in polygons)
            {
                Debug.Assert(IsEqual(polygon[0].Z, z), "expected horizontal polygons");
                a.Add(Flatten(polygon));
            }

            return a;
        }
        #endregion

        #region Geometrical Calculation
        public static List<XYZ> DividePoints(XYZ A, XYZ B, double length, double increment)
        {
            var retval = new List<XYZ>();
            retval.Add(A);
            retval.Add(B);

            // retval.Add(Midpoint(A, B))

            // ((Xa - Xb) / L) * Ld
            double newLength = increment;
            for (int i = 1, loopTo = (int)Math.Round(length / increment) - 1; i <= loopTo; i++)
            {
                double Xn = A.X + (B.X - A.X) / length * newLength;
                double Yn = A.Y + (B.Y - A.Y) / length * newLength;
                double Zn = A.Z + (B.Z - A.Z) / length * newLength;
                var pt = new XYZ(Xn, Yn, Zn);
                retval.Add(pt);
                newLength = newLength + increment;
            }

            return retval;
        }

        public static bool IsTopFace(Face f)
        {
            var b = f.GetBoundingBox();
            var p = b.Min;
            var q = b.Max;
            var midpoint = p + 0.5d * (q - p);
            var normal = f.ComputeNormal(midpoint);
            return PointsUpwards(normal);
        }

        public static bool IsBottomFace(Face f)
        {
            var b = f.GetBoundingBox();
            var p = b.Min;
            var q = b.Max;
            var midpoint = p + 0.5d * (q - p);
            var normal = f.ComputeNormal(midpoint);
            return PointsDownwards(normal);
        }
       
        /// <summary>
        /// Return arbitrary X and Y axes for the given 
        /// normal vector according to the AutoCAD 
        /// Arbitrary Axis Algorithm
        /// https://www.autodesk.com/techpubs/autocad/acadr14/dxf/arbitrary_axis_algorithm_al_u05_c.htm
        /// </summary>
        public static void GetArbitraryAxes(
          XYZ normal,
          out XYZ ax,
          out XYZ ay)
        {
            double limit = 1.0 / 64;

            XYZ pick_cardinal_axis
              = (IsZero(normal.X, limit)
                && IsZero(normal.Y, limit))
                  ? XYZ.BasisY
                  : XYZ.BasisZ;

            ax = pick_cardinal_axis.CrossProduct(normal).Normalize();
            ay = normal.CrossProduct(ax).Normalize();
        }

        /// <summary>
        /// Return the midpoint between two points.
        /// </summary>
        public static XYZ Midpoint(XYZ p, XYZ q)
        {
            return 0.5 * (p + q);
        }

        /// <summary>
        /// Return the midpoint of a Line.
        /// </summary>
        public static XYZ Midpoint(Line line)
        {
            return Midpoint(line.GetEndPoint(0),
              line.GetEndPoint(1));
        }

        /// <summary>
        /// Return the normal of a Line in the XY plane.
        /// </summary>
        public static XYZ Normal(Line line)
        {
            XYZ p = line.GetEndPoint(0);
            XYZ q = line.GetEndPoint(1);
            XYZ v = q - p;

            //Debug.Assert( IsZero( v.Z ),
            //  "expected horizontal line" );

            return v.CrossProduct(XYZ.BasisZ).Normalize();
        }

        /// <summary>
        /// Return the bounding box of a curve loop.
        /// </summary>
        public static BoundingBoxXYZ GetBoundingBox(
          CurveLoop curveLoop)
        {
            List<XYZ> pts = new List<XYZ>();
            foreach (Curve c in curveLoop)
            {
                pts.AddRange(c.Tessellate());
            }

            BoundingBoxXYZ bb = new BoundingBoxXYZ();
            bb.Clear();
            bb.ExpandToContain(pts);
            return bb;
        }

        /// <summary>
        /// Return the bottom four XYZ corners of the given 
        /// bounding box in the XY plane at the given 
        /// Z elevation in the order lower left, lower 
        /// right, upper right, upper left:
        /// </summary>
        public static XYZ[] GetBottomCorners(
          BoundingBoxXYZ b,
          double z)
        {
            return new XYZ[] {
        new XYZ( b.Min.X, b.Min.Y, z ),
        new XYZ( b.Max.X, b.Min.Y, z ),
        new XYZ( b.Max.X, b.Max.Y, z ),
        new XYZ( b.Min.X, b.Max.Y, z )
      };
        }

        /// <summary>
        /// Return the bottom four XYZ corners of the given 
        /// bounding box in the XY plane at the bb minimum 
        /// Z elevation in the order lower left, lower 
        /// right, upper right, upper left:
        /// </summary>
        public static XYZ[] GetBottomCorners(
          BoundingBoxXYZ b)
        {
            return GetBottomCorners(b, b.Min.Z);
        }

        #region Intersect
#if INTERSECT
    // from /a/src/cpp/wykobi/wykobi.inl
    // from https://github.com/ArashPartow/wykobi
    bool Intersect<T>( 
      T x1, T y1,
      T x2, T y2,
      T x3, T y3,
      T x4, T y4)
   {
      T ax = x2 - x1;
      T bx = x3 - x4;

    T lowerx;
    T upperx;
    T uppery;
    T lowery;

      if (ax<T(0.0))
      {
         lowerx = x2;
         upperx = x1;
      }
      else
      {
         upperx = x2;
         lowerx = x1;
      }

      if (bx > T(0.0))
      {
         if ((upperx<x4) || (x3<lowerx))
         return false;
      }
      else if ((upperx<x3) || (x4<lowerx))
         return false;

      const T ay = y2 - y1;
const T by = y3 - y4;

      if (ay<T(0.0))
      {
         lowery = y2;
         uppery = y1;
      }
      else
      {
         uppery = y2;
         lowery = y1;
      }

      if (by > T(0.0))
      {
         if ((uppery<y4) || (y3<lowery))
            return false;
      }
      else if ((uppery<y3) || (y4<lowery))
         return false;

      const T cx = x1 - x3;
const T cy = y1 - y3;
const T d = ( by * cx ) - ( bx * cy );
const T f = ( ay * bx ) - ( ax * by );

      if (f > T(0.0))
      {
         if ((d<T(0.0)) || (d > f))
            return false;
      }
      else if ((d > T(0.0)) || (d<f))
         return false;

      const T e = ( ax * cy ) - ( ay * cx );

      if (f > T(0.0))
      {
         if ((e<T(0.0)) || (e > f))
            return false;
      }
      else if ((e > T(0.0)) || (e<f))
         return false;

      return true;
   }

   bool Intersect<T>(T x1, T y1,
                         T x2, T y2,
                         T x3, T y3,
                         T x4, T y4,
                               out T ix, out T iy)
{
  const T ax = x2 - x1;
  const T bx = x3 - x4;

  T lowerx;
  T upperx;
  T uppery;
  T lowery;

  if( ax < T( 0.0 ) )
  {
    lowerx = x2;
    upperx = x1;
  }
  else
  {
    upperx = x2;
    lowerx = x1;
  }

  if( bx > T( 0.0 ) )
  {
    if( ( upperx < x4 ) || ( x3 < lowerx ) )
      return false;
  }
  else if( ( upperx < x3 ) || ( x4 < lowerx ) )
    return false;

  const T ay = y2 - y1;
  const T by = y3 - y4;

  if( ay < T( 0.0 ) )
  {
    lowery = y2;
    uppery = y1;
  }
  else
  {
    uppery = y2;
    lowery = y1;
  }

  if( by > T( 0.0 ) )
  {
    if( ( uppery < y4 ) || ( y3 < lowery ) )
      return false;
  }
  else if( ( uppery < y3 ) || ( y4 < lowery ) )
    return false;

  const T cx = x1 - x3;
  const T cy = y1 - y3;
  const T d = ( by * cx ) - ( bx * cy );
  const T f = ( ay * bx ) - ( ax * by );

  if( f > T( 0.0 ) )
  {
    if( ( d < T( 0.0 ) ) || ( d > f ) )
      return false;
  }
  else if( ( d > T( 0.0 ) ) || ( d < f ) )
    return false;

  const T e = ( ax * cy ) - ( ay * cx );

  if( f > T( 0.0 ) )
  {
    if( ( e < T( 0.0 ) ) || ( e > f ) )
      return false;
  }
  else if( ( e > T( 0.0 ) ) || ( e < f ) )
    return false;

  T ratio = ( ax * -by ) - ( ay * -bx );

  if( not_equal( ratio, T( 0.0 ) ) )
  {
    ratio = ( ( cy * -bx ) - ( cx * -by ) ) / ratio;
    ix = x1 + ( ratio * ax );
    iy = y1 + ( ratio * ay );
  }
  else
  {
    if( is_equal( ( ax * -cy ), ( -cx * ay ) ) )
    {
      ix = x3;
      iy = y3;
    }
    else
    {
      ix = x4;
      iy = y4;
    }
  }
  return true;
}
#endif // INTERSECT
        #endregion // Intersect

        /// <summary>
        /// Return the 2D intersection point between two 
        /// unbounded lines defined in the XY plane by the 
        /// given start and end points and vectors. 
        /// Return null if the two lines are coincident,
        /// in which case the intersection is an infinite 
        /// line, or non-coincident and parallel, in which 
        /// case it is empty.
        /// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
        /// </summary>
        public static XYZ LineLineIntersection(
          XYZ p1, XYZ v1, XYZ p2, XYZ v2)
        {
            XYZ w = p2 - p1;
            XYZ p5 = null;

            double c = (v2.X * w.Y - v2.Y * w.X)
              / (v2.X * v1.Y - v2.Y * v1.X);

            if (!double.IsInfinity(c))
            {
                double x = p1.X + c * v1.X;
                double y = p1.Y + c * v1.Y;

                p5 = new XYZ(x, y, 0);
            }
            return p5;
        }

        /// <summary>
        /// Return the 2D intersection point between two 
        /// unbounded lines defined in the XY plane by the 
        /// start and end points of the two given curves. 
        /// Return null if the two lines are coincident,
        /// in which case the intersection is an infinite 
        /// line, or non-coincident and parallel, in which 
        /// case it is empty.
        /// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
        /// </summary>
        public static XYZ LineLineIntersection(
          Curve c1,
          Curve c2)
        {
            XYZ p1 = c1.GetEndPoint(0);
            XYZ q1 = c1.GetEndPoint(1);
            XYZ p2 = c2.GetEndPoint(0);
            XYZ q2 = c2.GetEndPoint(1);
            XYZ v1 = q1 - p1;
            XYZ v2 = q2 - p2;
            return LineLineIntersection(p1, v1, p2, v2);
        }

        /// <summary>
        /// Return the 3D intersection point between
        /// a line and a plane.
        /// https://forums.autodesk.com/t5/revit-api-forum/how-can-we-calculate-the-intersection-between-the-plane-and-the/m-p/9785834
        /// https://stackoverflow.com/questions/5666222/3d-line-plane-intersection
        /// Determine the point of intersection between 
        /// a plane defined by a point and a normal vector 
        /// and a line defined by a point and a direction vector.
        /// planePoint - A point on the plane.
        /// planeNormal - The normal vector of the plane.
        /// linePoint - A point on the line.
        /// lineDirection - The direction vector of the line.
        /// lineParameter - The intersection distance along the line.
        /// Return - The point of intersection between the 
        /// line and the plane, null if the line is parallel 
        /// to the plane.
        /// </summary>
        public static XYZ LinePlaneIntersection(
          Line line,
          Plane plane,
          out double lineParameter)
        {
            XYZ planePoint = plane.Origin;
            XYZ planeNormal = plane.Normal;
            XYZ linePoint = line.GetEndPoint(0);

            XYZ lineDirection = (line.GetEndPoint(1)
              - linePoint).Normalize();

            // Is the line parallel to the plane, i.e.,
            // perpendicular to the plane normal?

            if (IsZero(planeNormal.DotProduct(lineDirection)))
            {
                lineParameter = double.NaN;
                return null;
            }

            lineParameter = (planeNormal.DotProduct(planePoint)
              - planeNormal.DotProduct(linePoint))
                / planeNormal.DotProduct(lineDirection);

            return linePoint + lineParameter * lineDirection;
        }

        /// <summary>
        /// Create transformation matrix to transform points 
        /// from the global space (XYZ) to the local space of 
        /// a face (UV representation of a bounding box).
        /// Revit itself only supports Face.Transform(UV) that 
        /// translates a UV coordinate into XYZ coordinate space. 
        /// I reversed that Method to translate XYZ coords to 
        /// UV coords. At first i thought i could solve the 
        /// reverse transformation by solving a linear equation 
        /// with 2 unknown variables. But this wasn't general. 
        /// I finally found out that the transformation 
        /// consists of a displacement vector and a rotation matrix.
        /// </summary>
        public static double[,]
          CalculateMatrixForGlobalToLocalCoordinateSystem(
            Face face)
        {
            // face.Evaluate uses a rotation matrix and
            // a displacement vector to translate points

            XYZ originDisplacementVectorUV = face.Evaluate(UV.Zero);
            XYZ unitVectorUWithDisplacement = face.Evaluate(UV.BasisU);
            XYZ unitVectorVWithDisplacement = face.Evaluate(UV.BasisV);

            XYZ unitVectorU = unitVectorUWithDisplacement
              - originDisplacementVectorUV;

            XYZ unitVectorV = unitVectorVWithDisplacement
              - originDisplacementVectorUV;

            // The rotation matrix A is composed of
            // unitVectorU and unitVectorV transposed.
            // To get the rotation matrix that translates from 
            // global space to local space, take the inverse of A.

            var a11i = unitVectorU.X;
            var a12i = unitVectorU.Y;
            var a21i = unitVectorV.X;
            var a22i = unitVectorV.Y;

            return new double[2, 2] {
        { a11i, a12i },
        { a21i, a22i }};
        }

        /// <summary>
        /// Create an arc in the XY plane from a given
        /// start point, end point and radius. 
        /// </summary>
        public static Arc CreateArc2dFromRadiusStartAndEndPoint(
          XYZ ps,
          XYZ pe,
          double radius,
          bool largeSagitta = false,
          bool clockwise = false)
        {
            // https://forums.autodesk.com/t5/revit-api-forum/create-a-curve-when-only-the-start-point-end-point-amp-radius-is/m-p/7830079

            XYZ midPointChord = 0.5 * (ps + pe);
            XYZ v = pe - ps;
            double d = 0.5 * v.GetLength(); // half chord length

            // Small and large circle sagitta:
            // http://www.mathopenref.com/sagitta.html
            // https://en.wikipedia.org/wiki/Sagitta_(geometry)

            double s = largeSagitta
              ? radius + Math.Sqrt(radius * radius - d * d) // sagitta large
              : radius - Math.Sqrt(radius * radius - d * d); // sagitta small

            XYZ midPointOffset = Transform
              .CreateRotation(XYZ.BasisZ, 0.5 * Math.PI)
              .OfVector(v.Normalize().Multiply(s));

            XYZ midPointArc = clockwise
              ? midPointChord + midPointOffset
              : midPointChord - midPointOffset;

            return Arc.Create(ps, pe, midPointArc);
        }

        /// <summary>
        /// Create a new CurveLoop from a list of points.
        /// </summary>
        public static CurveLoop CreateCurveLoop(
          List<XYZ> pts)
        {
            int n = pts.Count;
            CurveLoop curveLoop = new CurveLoop();
            for (int i = 1; i < n; ++i)
            {
                curveLoop.Append(Line.CreateBound(
                  pts[i - 1], pts[i]));
            }
            curveLoop.Append(Line.CreateBound(
              pts[n], pts[0]));
            return curveLoop;
        }

        /// <summary>
        /// Offset a list of points by a distance in a 
        /// given direction in or out of the curve loop.
        /// </summary>
        public static IEnumerable<XYZ> OffsetPoints(
          List<XYZ> pts,
          double offset,
          XYZ normal)
        {
            CurveLoop curveLoop = CreateCurveLoop(pts);

            CurveLoop curveLoop2 = CurveLoop.CreateViaOffset(
              curveLoop, offset, normal);

            return curveLoop2.Select<Curve, XYZ>(
                c => c.GetEndPoint(0));
        }
        #endregion // Geometrical Calculation

//        #region Colour Conversion
//        /// <summary>
//        /// Revit text colour parameter value stored as an integer 
//        /// in text note type BuiltInParameter.LINE_COLOR.
//        /// </summary>
//        public static int ToColorParameterValue(
//          int red,
//          int green,
//          int blue)
//        {
//            // from https://forums.autodesk.com/t5/revit-api-forum/how-to-change-text-color/td-p/2567672

//            int c = red + (green << 8) + (blue << 16);

//#if DEBUG
//            int c2 = red + 256 * green + 65536 * blue;
//            Debug.Assert(c == c2, "expected shift result to equal multiplication");
//#endif // DEBUG

//            return c;
//        }

//        /// <summary>
//        /// Revit text colour parameter value stored as an integer 
//        /// in text note type BuiltInParameter.LINE_COLOR.
//        /// </summary>
//        public static int GetRevitTextColorFromSystemColor(
//          System.Drawing.Color color)
//        {
//            // from https://forums.autodesk.com/t5/revit-api-forum/how-to-change-text-color/td-p/2567672

//            return ToColorParameterValue(color.R, color.G, color.B);
//        }
//        #endregion // Colour Conversion

//        #region Create Various Solids
//        /// <summary>
//        /// Create and return a solid sphere 
//        /// with a given radius and centre point.
//        /// </summary>
//        static public Solid CreateSphereAt(
//          XYZ centre,
//          double radius)
//        {
//            // Use the standard global coordinate system 
//            // as a frame, translated to the sphere centre.

//            Frame frame = new Frame(centre, XYZ.BasisX,
//              XYZ.BasisY, XYZ.BasisZ);

//            // Create a vertical half-circle loop 
//            // that must be in the frame location.

//            Arc arc = Arc.Create(
//              centre - radius * XYZ.BasisZ,
//              centre + radius * XYZ.BasisZ,
//              centre + radius * XYZ.BasisX);

//            Line line = Line.CreateBound(
//              arc.GetEndPoint(1),
//              arc.GetEndPoint(0));

//            CurveLoop halfCircle = new CurveLoop();
//            halfCircle.Append(arc);
//            halfCircle.Append(line);

//            List<CurveLoop> loops = new List<CurveLoop>(1);
//            loops.Add(halfCircle);

//            return GeometryCreationUtilities
//              .CreateRevolvedGeometry(frame, loops,
//                0, 2 * Math.PI);
//        }

//        /// <summary>
//        /// Create and return a cylinder
//        /// with a given origin, radius and axis.
//        /// Very similar to CreateCone!
//        /// </summary>
//        static public Solid CreateCylinder(
//          XYZ origin,
//          XYZ axis_vector,
//          double radius,
//          double height)
//        {
//            XYZ az = axis_vector.Normalize();

//            XYZ ax, ay;
//            GetArbitraryAxes(az, out ax, out ay);

//            // Define a rectangle in XZ plane

//            XYZ px = origin + radius * ax;
//            XYZ pxz = origin + radius * ax + height * az;
//            XYZ pz = origin + height * az;

//            List<Curve> profile = new List<Curve>();

//            profile.Add(Line.CreateBound(origin, px));
//            profile.Add(Line.CreateBound(px, pxz));
//            profile.Add(Line.CreateBound(pxz, pz));
//            profile.Add(Line.CreateBound(pz, origin));

//            CurveLoop curveLoop = CurveLoop.Create(profile);

//            Frame frame = new Frame(origin, ax, ay, az);

//            Solid cone = GeometryCreationUtilities
//              .CreateRevolvedGeometry(frame,
//                new CurveLoop[] { curveLoop },
//                0, 2 * Math.PI);

//            return cone;
//        }

//        /// <summary>
//        /// Create a cone-shaped solid at the given base
//        /// location pointing along the given axis.
//        /// Very similar to CreateCylinder!
//        /// </summary>
//        static public Solid CreateCone(
//          XYZ center,
//          XYZ axis_vector,
//          double radius,
//          double height)
//        {
//            XYZ az = axis_vector.Normalize();

//            XYZ ax, ay;
//            GetArbitraryAxes(az, out ax, out ay);

//            // Define a triangle in XZ plane

//            XYZ px = center + radius * ax;
//            XYZ pz = center + height * az;

//            List<Curve> profile = new List<Curve>();

//            profile.Add(Line.CreateBound(center, px));
//            profile.Add(Line.CreateBound(px, pz));
//            profile.Add(Line.CreateBound(pz, center));

//            CurveLoop curveLoop = CurveLoop.Create(profile);

//            Frame frame = new Frame(center, ax, ay, az);

//            //SolidOptions options = new SolidOptions( 
//            //  ElementId.InvalidElementId, 
//            //  ElementId.InvalidElementId );

//            Solid cone = GeometryCreationUtilities
//              .CreateRevolvedGeometry(frame,
//                new CurveLoop[] { curveLoop },
//                0, 2 * Math.PI);

//            return cone;

//            //using( Transaction t = new Transaction( Command.Doc, "Create cone" ) )
//            //{
//            //  t.Start();
//            //  DirectShape ds = DirectShape.CreateElement( Command.Doc, new ElementId( BuiltInCategory.OST_GenericModel ) );
//            //  ds.SetShape( new GeometryObject[] { cone } );
//            //  t.Commit();
//            //}
//        }

//        /// <summary>
//        /// Create a rotated arc shape for
//        /// https://forums.autodesk.com/t5/revit-api-forum/create-simple-solid-with-createrevolvedgeometry/m-p/10052114
//        /// </summary>
//        static public Solid CreateArcSolid(Arc arc)
//        {
//            XYZ p = arc.GetEndPoint(0);
//            XYZ q = arc.GetEndPoint(1);
//            XYZ r = q - q.Z * XYZ.BasisZ;

//            Frame frame = new Frame(r,
//              -XYZ.BasisX, -XYZ.BasisY, XYZ.BasisZ);

//            Line line2 = Line.CreateBound(q, r);
//            Line line3 = Line.CreateBound(r, p);

//            CurveLoop loop = new CurveLoop();
//            loop.Append(arc);
//            loop.Append(line2);
//            loop.Append(line3);

//            List<CurveLoop> loops = new List<CurveLoop>(1);
//            loops.Add(loop);

//            return GeometryCreationUtilities
//              .CreateRevolvedGeometry(frame,
//                loops, 0, 2 * Math.PI);
//        }

//        /// <summary>
//        /// Create and return a cube of 
//        /// side length d at the origin.
//        /// </summary>
//        static Solid CreateCube(double d)
//        {
//            return CreateRectangularPrism(
//              XYZ.Zero, d, d, d);
//        }

//        /// <summary>
//        /// Create and return a rectangular prism of the
//        /// given side lengths centered at the given point.
//        /// </summary>
//        static Solid CreateRectangularPrism(
//          XYZ center,
//          double d1,
//          double d2,
//          double d3)
//        {
//            List<Curve> profile = new List<Curve>();
//            XYZ profile00 = new XYZ(-d1 / 2, -d2 / 2, -d3 / 2);
//            XYZ profile01 = new XYZ(-d1 / 2, d2 / 2, -d3 / 2);
//            XYZ profile11 = new XYZ(d1 / 2, d2 / 2, -d3 / 2);
//            XYZ profile10 = new XYZ(d1 / 2, -d2 / 2, -d3 / 2);

//            profile.Add(Line.CreateBound(profile00, profile01));
//            profile.Add(Line.CreateBound(profile01, profile11));
//            profile.Add(Line.CreateBound(profile11, profile10));
//            profile.Add(Line.CreateBound(profile10, profile00));

//            CurveLoop curveLoop = CurveLoop.Create(profile);

//            SolidOptions options = new SolidOptions(
//              ElementId.InvalidElementId,
//              ElementId.InvalidElementId);

//            return GeometryCreationUtilities
//              .CreateExtrusionGeometry(
//                new CurveLoop[] { curveLoop },
//                XYZ.BasisZ, d3, options);
//        }

//        /// <summary>
//        /// Create and return a solid representing 
//        /// the bounding box of the input solid.
//        /// Assumption: aligned with Z axis.
//        /// Written, described and tested by Owen Merrick for 
//        /// http://forums.autodesk.com/t5/revit-api-forum/create-solid-from-boundingbox/m-p/6592486
//        /// </summary>
//        public static Solid CreateSolidFromBoundingBox(
//          Solid inputSolid)
//        {
//            BoundingBoxXYZ bbox = inputSolid.GetBoundingBox();

//            // Corners in BBox coords

//            XYZ pt0 = new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
//            XYZ pt1 = new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
//            XYZ pt2 = new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
//            XYZ pt3 = new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);

//            // Edges in BBox coords

//            Line edge0 = Line.CreateBound(pt0, pt1);
//            Line edge1 = Line.CreateBound(pt1, pt2);
//            Line edge2 = Line.CreateBound(pt2, pt3);
//            Line edge3 = Line.CreateBound(pt3, pt0);

//            // Create loop, still in BBox coords

//            List<Curve> edges = new List<Curve>();
//            edges.Add(edge0);
//            edges.Add(edge1);
//            edges.Add(edge2);
//            edges.Add(edge3);

//            double height = bbox.Max.Z - bbox.Min.Z;

//            CurveLoop baseLoop = CurveLoop.Create(edges);

//            List<CurveLoop> loopList = new List<CurveLoop>();
//            loopList.Add(baseLoop);

//            Solid preTransformBox = GeometryCreationUtilities
//              .CreateExtrusionGeometry(loopList, XYZ.BasisZ,
//                height);

//            Solid transformBox = SolidUtils.CreateTransformed(
//              preTransformBox, bbox.Transform);

//            return transformBox;
//        }
//        #endregion // Create Various Solids

//        #region Convex Hull
//        /// <summary>
//        /// Return the convex hull of a list of points 
//        /// using the Jarvis march or Gift wrapping:
//        /// https://en.wikipedia.org/wiki/Gift_wrapping_algorithm
//        /// Written by Maxence.
//        /// </summary>
//        public static List<XYZ> ConvexHull(List<XYZ> points)
//        {
//            if (points == null)
//                throw new ArgumentNullException(nameof(points));
//            XYZ startPoint = points.MinBy(p => p.X);
//            var convexHullPoints = new List<XYZ>();
//            XYZ walkingPoint = startPoint;
//            XYZ refVector = XYZ.BasisY.Negate();
//            do
//            {
//                convexHullPoints.Add(walkingPoint);
//                XYZ wp = walkingPoint;
//                XYZ rv = refVector;
//                walkingPoint = points.MinBy(p =>
//                {
//                    double angle = (p - wp).AngleOnPlaneTo(rv, XYZ.BasisZ);
//                    if (angle < 1e-10)
//                        angle = 2 * Math.PI;
//                    return angle;
//                });
//                refVector = wp - walkingPoint;
//            } while (walkingPoint != startPoint);
//            convexHullPoints.Reverse();
//            return convexHullPoints;
//        }
//        #endregion // Convex Hull

        #region Unit Handling
        /// <summary>
        /// Base units currently used internally by Revit.
        /// </summary>
        enum BaseUnit
        {
            BU_Length = 0,         // length, feet (ft)
            BU_Angle,              // angle, radian (rad)
            BU_Mass,               // mass, kilogram (kg)
            BU_Time,               // time, second (s)
            BU_Electric_Current,   // electric current, ampere (A)
            BU_Temperature,        // temperature, kelvin (K)
            BU_Luminous_Intensity, // luminous intensity, candela (cd)
            BU_Solid_Angle,        // solid angle, steradian (sr)

            NumBaseUnits
        };

        const double _inchToMm = 25.4;
        const double _footToMm = 12 * _inchToMm;
        const double _footToMeter = _footToMm * 0.001;
        const double _sqfToSqm = _footToMeter * _footToMeter;
        const double _cubicFootToCubicMeter = _footToMeter * _sqfToSqm;

        /// <summary>
        /// Convert a given length in feet to millimetres.
        /// </summary>
        public static double FootToMm(double length)
        {
            return length * _footToMm;
        }

        /// <summary>
        /// Convert a given length in feet to millimetres,
        /// rounded to the closest millimetre.
        /// </summary>
        public static int FootToMmInt(double length)
        {
            //return (int) ( _feet_to_mm * d + 0.5 );
            return (int)Math.Round(_footToMm * length,
              MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Convert a given length in feet to metres.
        /// </summary>
        public static double FootToMetre(double length)
        {
            return length * _footToMeter;
        }

        /// <summary>
        /// Convert a given length in millimetres to feet.
        /// </summary>
        public static double MmToFoot(double length)
        {
            return length / _footToMm;
        }

        /// <summary>
        /// Convert a given point or vector from millimetres to feet.
        /// </summary>
        public static XYZ MmToFoot(XYZ v)
        {
            return v.Divide(_footToMm);
        }

        /// <summary>
        /// Convert a given volume in feet to cubic meters.
        /// </summary>
        public static double CubicFootToCubicMeter(double volume)
        {
            return volume * _cubicFootToCubicMeter;
        }

        /// <summary>
        /// Hard coded abbreviations for the first 26
        /// DisplayUnitType enumeration values.
        /// </summary>
        public static string[] DisplayUnitTypeAbbreviation
          = new string[] {
      "m", // DUT_METERS = 0,
      "cm", // DUT_CENTIMETERS = 1,
      "mm", // DUT_MILLIMETERS = 2,
      "ft", // DUT_DECIMAL_FEET = 3,
      "N/A", // DUT_FEET_FRACTIONAL_INCHES = 4,
      "N/A", // DUT_FRACTIONAL_INCHES = 5,
      "in", // DUT_DECIMAL_INCHES = 6,
      "ac", // DUT_ACRES = 7,
      "ha", // DUT_HECTARES = 8,
      "N/A", // DUT_METERS_CENTIMETERS = 9,
      "y^3", // DUT_CUBIC_YARDS = 10,
      "ft^2", // DUT_SQUARE_FEET = 11,
      "m^2", // DUT_SQUARE_METERS = 12,
      "ft^3", // DUT_CUBIC_FEET = 13,
      "m^3", // DUT_CUBIC_METERS = 14,
      "deg", // DUT_DECIMAL_DEGREES = 15,
      "N/A", // DUT_DEGREES_AND_MINUTES = 16,
      "N/A", // DUT_GENERAL = 17,
      "N/A", // DUT_FIXED = 18,
      "%", // DUT_PERCENTAGE = 19,
      "in^2", // DUT_SQUARE_INCHES = 20,
      "cm^2", // DUT_SQUARE_CENTIMETERS = 21,
      "mm^2", // DUT_SQUARE_MILLIMETERS = 22,
      "in^3", // DUT_CUBIC_INCHES = 23,
      "cm^3", // DUT_CUBIC_CENTIMETERS = 24,
      "mm^3", // DUT_CUBIC_MILLIMETERS = 25,
      "l" // DUT_LITERS = 26,
          };

        /// <summary>
        /// List all Forge type ids
        /// </summary>
        /// <param name="doc"></param>
        public static void ListForgeTypeIds()
        {
            //ForgeTypeId a = SpecTypeId.Acceleration;
            //Debug.Print( a.TypeId );

            Type spityp = typeof(SpecTypeId);

            //foreach( MemberInfo mi in spityp.GetMembers() )
            //{
            //  Debug.Print( mi.Name );
            //}

            PropertyInfo[] ps = spityp.GetProperties(
              BindingFlags.Public | BindingFlags.Static);

            // Sort properties alphabetically by name 

            Array.Sort(ps,
              delegate (PropertyInfo p1, PropertyInfo p2)
              { return p1.Name.CompareTo(p2.Name); });

            Debug.Print("{0} properties:", ps.Length);

            foreach (PropertyInfo pi in ps)
            {
                if (pi.PropertyType == typeof(ForgeTypeId))
                {
                    object obj = pi.GetValue(null, null);

                    ForgeTypeId fti = obj as ForgeTypeId;

                    Debug.Print("{0}: {1}", pi.Name, fti.TypeId);
                }
            }

            IList<ForgeTypeId> specs = UnitUtils.GetAllSpecs();

            Debug.Print("{0} specs:", specs.Count);

            foreach (ForgeTypeId fti in specs)
            {
                Debug.Print("{0}: {1}, {2}",
                  fti.ToString(), fti.TypeId,
                  UnitUtils.GetTypeCatalogStringForSpec(fti));
            }

            IList<ForgeTypeId> units = UnitUtils.GetAllUnits();

            Debug.Print("{0} units:", units.Count);

            foreach (ForgeTypeId fti in units)
            {
                Debug.Print("{0}: {1}, {2}",
                  fti.ToString(), fti.TypeId,
                  UnitUtils.GetTypeCatalogStringForUnit(fti));
            }
        }
        #endregion // Unit Handling

        #region Formatting
        /// <summary>
        /// Return an English plural suffix for the given
        /// number of items, i.e. 's' for zero or more
        /// than one, and nothing for exactly one.
        /// </summary>
        public static string PluralSuffix(int n)
        {
            return 1 == n ? "" : "s";
        }

        /// <summary>
        /// Return an English plural suffix 'ies' or
        /// 'y' for the given number of items.
        /// </summary>
        public static string PluralSuffixY(int n)
        {
            return 1 == n ? "y" : "ies";
        }

        /// <summary>
        /// Return a dot (full stop) for zero
        /// or a colon for more than zero.
        /// </summary>
        public static string DotOrColon(int n)
        {
            return 0 < n ? ":" : ".";
        }

        /// <summary>
        /// Return a string for a real number
        /// formatted to two decimal places.
        /// </summary>
        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }

        /// <summary>
        /// Return a hash string for a real number
        /// formatted to nine decimal places.
        /// </summary>
        public static string HashString(double a)
        {
            return a.ToString("0.#########");
        }

        /// <summary>
        /// Return a string representation in degrees
        /// for an angle given in radians.
        /// </summary>
        public static string AngleString(double angle)
        {
            return RealString(angle * 180 / Math.PI)
              + " degrees";
        }

        /// <summary>
        /// Return a string for a length in millimetres
        /// formatted as an integer value.
        /// </summary>
        public static string MmString(double length)
        {
            //return RealString( FootToMm( length ) ) + " mm";
            return Math.Round(FootToMm(length))
              .ToString() + " mm";
        }

        /// <summary>
        /// Return a string for a UV point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(
          UV p,
          bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator
              ? "{0} {1}"
              : "({0},{1})";

            return string.Format(format_string,
              RealString(p.U),
              RealString(p.V));
        }

        /// <summary>
        /// Return a string for an XYZ point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(
          XYZ p,
          bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator
              ? "{0} {1} {2}"
              : "({0},{1},{2})";

            return string.Format(format_string,
              RealString(p.X),
              RealString(p.Y),
              RealString(p.Z));
        }

        /// <summary>
        /// Return a hash string for an XYZ point
        /// or vector with its coordinates
        /// formatted to nine decimal places.
        /// </summary>
        public static string HashString(XYZ p)
        {
            return string.Format("({0},{1},{2})",
              HashString(p.X),
              HashString(p.Y),
              HashString(p.Z));
        }

        /// <summary>
        /// Return a string for this bounding box
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string BoundingBoxString(
          BoundingBoxUV bb,
          bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator
              ? "{0} {1}"
              : "({0},{1})";

            return string.Format(format_string,
              PointString(bb.Min, onlySpaceSeparator),
              PointString(bb.Max, onlySpaceSeparator));
        }

        /// <summary>
        /// Return a string for this bounding box
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string BoundingBoxString(
          BoundingBoxXYZ bb,
          bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator
              ? "{0} {1}"
              : "({0},{1})";

            return string.Format(format_string,
              PointString(bb.Min, onlySpaceSeparator),
              PointString(bb.Max, onlySpaceSeparator));
        }

        /// <summary>
        /// Return a string for this plane
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PlaneString(Plane p)
        {
            return string.Format("plane origin {0}, plane normal {1}",
              PointString(p.Origin),
              PointString(p.Normal));
        }

        /// <summary>
        /// Return a string for this transformation
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string TransformString(Transform t)
        {
            return string.Format("({0},{1},{2},{3})", PointString(t.Origin),
              PointString(t.BasisX), PointString(t.BasisY), PointString(t.BasisZ));
        }

        /// <summary>
        /// Return a string for a list of doubles 
        /// formatted to two decimal places.
        /// </summary>
        public static string DoubleArrayString(
          IEnumerable<double> a,
          bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator
              ? " "
              : ", ";

            return string.Join(separator,
              a.Select<double, string>(
                x => RealString(x)));
        }

        /// <summary>
        /// Return a string for this point array
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PointArrayString(
          IEnumerable<UV> pts,
          bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator
              ? " "
              : ", ";

            return string.Join(separator,
              pts.Select<UV, string>(p
               => PointString(p, onlySpaceSeparator)));
        }

        /// <summary>
        /// Return a string for this point array
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PointArrayString(
          IEnumerable<XYZ> pts,
          bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator
              ? " "
              : ", ";

            return string.Join(separator,
              pts.Select<XYZ, string>(p
               => PointString(p, onlySpaceSeparator)));
        }

        /// <summary>
        /// Return a string representing the data of a
        /// curve. Currently includes detailed data of
        /// line and arc elements only.
        /// </summary>
        public static string CurveString(Curve c)
        {
            string s = c.GetType().Name.ToLower();

            XYZ p = c.GetEndPoint(0);
            XYZ q = c.GetEndPoint(1);

            s += string.Format(" {0} --> {1}",
              PointString(p), PointString(q));

            // To list intermediate points or draw an
            // approximation using straight line segments,
            // we can access the curve tesselation, cf.
            // CurveTessellateString:

            //foreach( XYZ r in lc.Curve.Tessellate() )
            //{
            //}

            // List arc data:

            Arc arc = c as Arc;

            if (null != arc)
            {
                s += string.Format(" center {0} radius {1}",
                  PointString(arc.Center), arc.Radius);
            }

            // Todo: add support for other curve types
            // besides line and arc.

            return s;
        }

        /// <summary>
        /// Return a string for this curve with its
        /// tessellated point coordinates formatted
        /// to two decimal places.
        /// </summary>
        public static string CurveTessellateString(
          Curve curve)
        {
            return "curve tessellation "
              + PointArrayString(curve.Tessellate());
        }

        #region Using Obsolete pre-Forge Unit API Functionality Deprecated in Revit 2021
#if USE_PRE_FORGE_UNIT_FUNCTIONALITY
    /// <summary>
    /// Convert a UnitSymbolType enumeration value
    /// to a brief human readable abbreviation string.
    /// </summary>
    public static string UnitSymbolTypeString(
      UnitSymbolType u )
    {
      string s = u.ToString();

      Debug.Assert( s.StartsWith( "UST_" ),
        "expected UnitSymbolType enumeration value "
        + "to begin with 'UST_'" );

      s = s.Substring( 4 )
        .Replace( "_SUP_", "^" )
        .ToLower();

      return s;
    }
#endif // USE_PRE_FORGE_UNIT_FUNCTIONALITY
        #endregion // Using Obsolete pre-Forge Unit API Functionality Deprecated in Revit 2021
        #endregion // Formatting

        #region Display a message
        const string _caption = "The Building Coder";

        public static void InfoMsg(string msg)
        {
            Debug.WriteLine(msg);
            WinForms.MessageBox.Show(msg,
              _caption,
              WinForms.MessageBoxButtons.OK,
              WinForms.MessageBoxIcon.Information);
        }

        public static void InfoMsg2(
          string instruction,
          string content)
        {
            Debug.WriteLine(instruction + "\r\n" + content);
            TaskDialog d = new TaskDialog(_caption);
            d.MainInstruction = instruction;
            d.MainContent = content;
            d.Show();
        }

        public static void ErrorMsg(string msg)
        {
            Debug.WriteLine(msg);
            WinForms.MessageBox.Show(msg,
              _caption,
              WinForms.MessageBoxButtons.OK,
              WinForms.MessageBoxIcon.Error);
        }

        /// <summary>
        /// Return a string describing the given element:
        /// .NET type name,
        /// category name,
        /// family and symbol name for a family instance,
        /// element id and element name.
        /// </summary>
        public static string ElementDescription(
          Element e)
        {
            if (null == e)
            {
                return "<null>";
            }

            // For a wall, the element name equals the
            // wall type name, which is equivalent to the
            // family name ...

            FamilyInstance fi = e as FamilyInstance;

            string typeName = e.GetType().Name;

            string categoryName = (null == e.Category)
              ? string.Empty
              : e.Category.Name + " ";

            string familyName = (null == fi)
              ? string.Empty
              : fi.Symbol.Family.Name + " ";

            string symbolName = (null == fi
              || e.Name.Equals(fi.Symbol.Name))
                ? string.Empty
                : fi.Symbol.Name + " ";

            return string.Format("{0} {1}{2}{3}<{4} {5}>",
              typeName, categoryName, familyName,
              symbolName, e.Id.IntegerValue, e.Name);
        }

        /// <summary>
        /// Return a location for the given element using
        /// its LocationPoint Point property,
        /// LocationCurve start point, whichever 
        /// is available.
        /// </summary>
        /// <param name="p">Return element location point</param>
        /// <param name="e">Revit Element</param>
        /// <returns>True if a location point is available 
        /// for the given element, otherwise false.</returns>
        static public bool GetElementLocation(
          out XYZ p,
          Element e)
        {
            p = XYZ.Zero;
            bool rc = false;
            Location loc = e.Location;
            if (null != loc)
            {
                LocationPoint lp = loc as LocationPoint;
                if (null != lp)
                {
                    p = lp.Point;
                    rc = true;
                }
                else
                {
                    LocationCurve lc = loc as LocationCurve;

                    Debug.Assert(null != lc,
                      "expected location to be either point or curve");

                    p = lc.Curve.GetEndPoint(0);
                    rc = true;
                }
            }
            return rc;
        }

        /// <summary>
        /// Return the location point of a family instance or null.
        /// This null coalesces the location so you won't get an 
        /// error if the FamilyInstance is an invalid object.  
        /// </summary>
        public static XYZ GetFamilyInstanceLocation(
          FamilyInstance fi)
        {
            return ((LocationPoint)fi?.Location)?.Point;
        }
        #endregion // Display a message

        #region Element Selection
        public static Element SelectSingleElement(
          UIDocument uidoc,
          string description)
        {
            if (ViewType.Internal == uidoc.ActiveView.ViewType)
            {
                TaskDialog.Show("Error",
                  "Cannot pick element in this view: "
                  + uidoc.ActiveView.Name);

                return null;
            }

#if _2010
    sel.Elements.Clear();
    Element e = null;
    sel.StatusbarTip = "Please select " + description;
    if( sel.PickOne() )
    {
      ElementSetIterator elemSetItr
        = sel.Elements.ForwardIterator();
      elemSetItr.MoveNext();
      e = elemSetItr.Current as Element;
    }
    return e;
#endif // _2010

            try
            {
                Reference r = uidoc.Selection.PickObject(
                  ObjectType.Element,
                  "Please select " + description);

                // 'Autodesk.Revit.DB.Reference.Element' is
                // obsolete: Property will be removed. Use
                // Document.GetElement(Reference) instead.
                //return null == r ? null : r.Element; // 2011

                return uidoc.Document.GetElement(r); // 2012
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        public static Element GetSingleSelectedElement(
          UIDocument uidoc)
        {
            ICollection<ElementId> ids
              = uidoc.Selection.GetElementIds();

            Element e = null;

            if (1 == ids.Count)
            {
                foreach (ElementId id in ids)
                {
                    e = uidoc.Document.GetElement(id);
                }
            }
            return e;
        }

        static bool HasRequestedType(
          Element e,
          Type t,
          bool acceptDerivedClass)
        {
            bool rc = null != e;

            if (rc)
            {
                Type t2 = e.GetType();

                rc = t2.Equals(t);

                if (!rc && acceptDerivedClass)
                {
                    rc = t2.IsSubclassOf(t);
                }
            }
            return rc;
        }

        public static Element SelectSingleElementOfType(
          UIDocument uidoc,
          Type t,
          string description,
          bool acceptDerivedClass)
        {
            Element e = GetSingleSelectedElement(uidoc);

            if (!HasRequestedType(e, t, acceptDerivedClass))
            {
                e = Util.SelectSingleElement(
                  uidoc, description);
            }
            return HasRequestedType(e, t, acceptDerivedClass)
              ? e
              : null;
        }

        /// <summary>
        /// Retrieve all pre-selected elements of the specified type,
        /// if any elements at all have been pre-selected. If not,
        /// retrieve all elements of specified type in the database.
        /// </summary>
        /// <param name="a">Return value container</param>
        /// <param name="uidoc">Active document</param>
        /// <param name="t">Specific type</param>
        /// <returns>True if some elements were retrieved</returns>
        public static bool GetSelectedElementsOrAll(
          List<Element> a,
          UIDocument uidoc,
          Type t)
        {
            Document doc = uidoc.Document;

            ICollection<ElementId> ids
              = uidoc.Selection.GetElementIds();

            if (0 < ids.Count)
            {
                a.AddRange(ids
                  .Select<ElementId, Element>(
                    id => doc.GetElement(id))
                  .Where<Element>(
                    e => t.IsInstanceOfType(e)));
            }
            else
            {
                a.AddRange(new FilteredElementCollector(doc)
                  .OfClass(t));
            }
            return 0 < a.Count;
        }
        #endregion // Element Selection

        #region Element Filtering
        /// <summary>
        /// Return all elements of the requested class i.e. System.Type
        /// matching the given built-in category in the given document.
        /// </summary>
        public static FilteredElementCollector GetElementsOfType(
          Document doc,
          Type type,
          BuiltInCategory bic)
        {
            FilteredElementCollector collector
              = new FilteredElementCollector(doc);

            collector.OfCategory(bic);
            collector.OfClass(type);

            return collector;
        }

        /// <summary>
        /// Return the first element of the given type and name.
        /// </summary>
        public static Element GetFirstElementOfTypeNamed(
          Document doc,
          Type type,
          string name)
        {
            FilteredElementCollector collector
              = new FilteredElementCollector(doc)
                .OfClass(type);

#if EXPLICIT_CODE

      // explicit iteration and manual checking of a property:

      Element ret = null;
      foreach( Element e in collector )
      {
        if( e.Name.Equals( name ) )
        {
          ret = e;
          break;
        }
      }
      return ret;
#endif // EXPLICIT_CODE

#if USE_LINQ

      // using LINQ:

      IEnumerable<Element> elementsByName =
        from e in collector
        where e.Name.Equals( name )
        select e;

      return elementsByName.First<Element>();
#endif // USE_LINQ

            // using an anonymous method:

            // if no matching elements exist, First<> throws an exception.

            //return collector.Any<Element>( e => e.Name.Equals( name ) )
            //  ? collector.First<Element>( e => e.Name.Equals( name ) )
            //  : null;

            // using an anonymous method to define a named method:

            Func<Element, bool> nameEquals = e => e.Name.Equals(name);

            return collector.Any<Element>(nameEquals)
              ? collector.First<Element>(nameEquals)
              : null;
        }

        /// <summary>
        /// Return the first 3D view which is not a template,
        /// useful for input to FindReferencesByDirection().
        /// In this case, one cannot use FirstElement() directly,
        /// since the first one found may be a template and
        /// unsuitable for use in this method.
        /// This demonstrates some interesting usage of
        /// a .NET anonymous method.
        /// </summary>
        public static Element GetFirstNonTemplate3dView(Document doc)
        {
            FilteredElementCollector collector
              = new FilteredElementCollector(doc);

            collector.OfClass(typeof(View3D));

            return collector
              .Cast<View3D>()
              .First<View3D>(v3 => !v3.IsTemplate);
        }

        /// <summary>
        /// Given a specific family and symbol name,
        /// return the appropriate family symbol.
        /// </summary>
        public static FamilySymbol FindFamilySymbol(
          Document doc,
          string familyName,
          string symbolName)
        {
            FilteredElementCollector collector
              = new FilteredElementCollector(doc)
                .OfClass(typeof(Family));

            foreach (Family f in collector)
            {
                if (f.Name.Equals(familyName))
                {
                    //foreach( FamilySymbol symbol in f.Symbols ) // 2014

                    ISet<ElementId> ids = f.GetFamilySymbolIds(); // 2015

                    foreach (ElementId id in ids)
                    {
                        FamilySymbol symbol = doc.GetElement(id)
                          as FamilySymbol;

                        if (symbol.Name == symbolName)
                        {
                            return symbol;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Return the first element type matching the given name.
        /// This filter could be speeded up by using a (quick)
        /// parameter filter instead of the (slower than slow)
        /// LINQ post-processing.
        /// </summary>
        public static ElementType GetElementTypeByName(
          Document doc,
          string name)
        {
            return new FilteredElementCollector(doc)
              .OfClass(typeof(ElementType))
              .First(q => q.Name.Equals(name))
                as ElementType;
        }

        /// <summary>
        /// Return the first family symbol matching the given name.
        /// Note that FamilySymbol is a subclass of ElementType,
        /// so this method is more restrictive above all faster
        /// than the previous one.
        /// This filter could be speeded up by using a (quick)
        /// parameter filter instead of the (slower than slow)
        /// LINQ post-processing.
        /// </summary>
        public static ElementType GetFamilySymbolByName(
          Document doc,
          string name)
        {
            return new FilteredElementCollector(doc)
              .OfClass(typeof(FamilySymbol))
              .First(q => q.Name.Equals(name))
                as FamilySymbol;
        }
        #endregion // Element Filtering

    }

    #region Extension Method Classes

    public static class IEnumerableExtensions
    {
        // (C) Jonathan Skeet
        // from https://github.com/morelinq/MoreLINQ/blob/master/MoreLinq/MinBy.cs
        public static tsource MinBy<tsource, tkey>(
          this IEnumerable<tsource> source,
          Func<tsource, tkey> selector)
        {
            return source.MinBy(selector, Comparer<tkey>.Default);
        }

        public static tsource MinBy<tsource, tkey>(
          this IEnumerable<tsource> source,
          Func<tsource, tkey> selector,
          IComparer<tkey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            using (IEnumerator<tsource> sourceIterator = source.GetEnumerator())
            {
                if (!sourceIterator.MoveNext())
                    throw new InvalidOperationException("Sequence was empty");
                tsource min = sourceIterator.Current;
                tkey minKey = selector(min);
                while (sourceIterator.MoveNext())
                {
                    tsource candidate = sourceIterator.Current;
                    tkey candidateProjected = selector(candidate);
                    if (comparer.Compare(candidateProjected, minKey) < 0)
                    {
                        min = candidate;
                        minKey = candidateProjected;
                    }
                }
                return min;
            }
        }

        /// <summary>
        /// Create HashSet from IEnumerable given selector and comparer.
        /// http://geekswithblogs.net/BlackRabbitCoder/archive/2011/03/31/c.net-toolbox-adding-a-tohashset-extension-method.aspx
        /// </summary>
        public static HashSet<TElement> ToHashSet<TSource, TElement>(
          this IEnumerable<TSource> source,
          Func<TSource, TElement> elementSelector,
          IEqualityComparer<TElement> comparer)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (elementSelector == null)
                throw new ArgumentNullException("elementSelector");

            // you can unroll this into a foreach if you want efficiency gain, but for brevity...
            return new HashSet<TElement>(
              source.Select(elementSelector), comparer);
        }

        /// <summary>
        /// Create a HashSet of TSource from an IEnumerable 
        /// of TSource using the identity selector and 
        /// default equality comparer.
        /// </summary>
        public static HashSet<TSource> ToHashSet<TSource>(
          this IEnumerable<TSource> source)
        {
            // key selector is identity fxn and null is default comparer
            return source.ToHashSet<TSource, TSource>(
              item => item, null);
        }

        /// <summary>
        /// Create a HashSet of TSource from an IEnumerable 
        /// of TSource using the identity selector and 
        /// specified equality comparer.
        /// </summary>
        public static HashSet<TSource> ToHashSet<TSource>(
          this IEnumerable<TSource> source,
          IEqualityComparer<TSource> comparer)
        {
            return source.ToHashSet<TSource, TSource>(
              item => item, comparer);
        }

        /// <summary>
        /// Create a HashSet of TElement from an IEnumerable 
        /// of TSource using the specified element selector 
        /// and default equality comparer.
        /// </summary>
        public static HashSet<TElement> ToHashSet<TSource, TElement>(
          this IEnumerable<TSource> source,
          Func<TSource, TElement> elementSelector)
        {
            return source.ToHashSet<TSource, TElement>(
              elementSelector, null);
        }
    }

    public static class JtElementExtensionMethods
    {
        /// <summary>
        /// Predicate to determine whether given element 
        /// is a physical element, i.e. valid category,
        /// not view specific, etc.
        /// </summary>
        public static bool IsPhysicalElement(
          this Element e)
        {
            if (e.Category == null)
                return false;
            // does this produce same result as 
            // WhereElementIsViewIndependent ?
            if (e.ViewSpecific)
                return false;
            // exclude specific unwanted categories
            if (((BuiltInCategory)e.Category.Id.IntegerValue)
              == BuiltInCategory.OST_HVAC_Zones)
            {
                return false;
            }
            return e.Category.CategoryType == CategoryType.Model
              && e.Category.CanAddSubcategory;
        }

        /// <summary>
        /// Return the curve from a Revit database Element 
        /// location curve, if it has one.
        /// </summary>
        public static Curve GetCurve(this Element e)
        {
            Debug.Assert(null != e.Location,
              "expected an element with a valid Location");

            LocationCurve lc = e.Location as LocationCurve;

            Debug.Assert(null != lc,
              "expected an element with a valid LocationCurve");

            return lc.Curve;
        }
    }

    public static class JtElementIdExtensionMethods
    {
        /// <summary>
        /// Predicate returning true for invalid element ids.
        /// </summary>
        public static bool IsInvalid(this ElementId id)
        {
            return ElementId.InvalidElementId == id;
        }
        /// <summary>
        /// Predicate returning true for valid element ids.
        /// </summary>
        public static bool IsValid(this ElementId id)
        {
            return !IsInvalid(id);
        }
    }

    public static class JtLineExtensionMethods
    {
        /// <summary>
        /// Return true if the given point is very close 
        /// to this line, within a very narrow ellipse
        /// whose focal points are the line start and end.
        /// The tolerance is defined as (1 - e) using the 
        /// eccentricity e. e = 0 means we have a circle; 
        /// The closer e is to 1, the more elongated the 
        /// shape of the ellipse.
        /// https://en.wikipedia.org/wiki/Ellipse#Eccentricity
        /// </summary>
        public static bool Contains(
          this Line line,
          XYZ p,
          double tolerance = Util._eps)
        {
            XYZ a = line.GetEndPoint(0); // line start point
            XYZ b = line.GetEndPoint(1); // line end point
            double f = a.DistanceTo(b); // distance between focal points
            double da = a.DistanceTo(p);
            double db = p.DistanceTo(b);
            // da + db is always greater or equal f
            return ((da + db) - f) * f < tolerance;
        }
    }

    public static class JtBoundingBoxXyzExtensionMethods
    {
        /// <summary>
        /// Make this bounding box empty by setting the
        /// Min value to plus infinity and Max to minus.
        /// </summary>
        public static void Clear(
          this BoundingBoxXYZ bb)
        {
            double infinity = double.MaxValue;
            bb.Min = new XYZ(infinity, infinity, infinity);
            bb.Max = -bb.Min;
        }

        /// <summary>
        /// Expand the given bounding box to include 
        /// and contain the given point.
        /// </summary>
        public static void ExpandToContain(
          this BoundingBoxXYZ bb,
          XYZ p)
        {
            bb.Min = new XYZ(Math.Min(bb.Min.X, p.X),
              Math.Min(bb.Min.Y, p.Y),
              Math.Min(bb.Min.Z, p.Z));

            bb.Max = new XYZ(Math.Max(bb.Max.X, p.X),
              Math.Max(bb.Max.Y, p.Y),
              Math.Max(bb.Max.Z, p.Z));
        }

        /// <summary>
        /// Expand the given bounding box to include 
        /// and contain the given points.
        /// </summary>
        public static void ExpandToContain(
          this BoundingBoxXYZ bb,
          IEnumerable<XYZ> pts)
        {
            bb.ExpandToContain(new XYZ(
              pts.Min<XYZ, double>(p => p.X),
              pts.Min<XYZ, double>(p => p.Y),
              pts.Min<XYZ, double>(p => p.Z)));

            bb.ExpandToContain(new XYZ(
              pts.Max<XYZ, double>(p => p.X),
              pts.Max<XYZ, double>(p => p.Y),
              pts.Max<XYZ, double>(p => p.Z)));
        }

        /// <summary>
        /// Expand the given bounding box to include 
        /// and contain the given other one.
        /// </summary>
        public static void ExpandToContain(
          this BoundingBoxXYZ bb,
          BoundingBoxXYZ other)
        {
            bb.ExpandToContain(other.Min);
            bb.ExpandToContain(other.Max);
        }
    }

    public static class JtPlaneExtensionMethods
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
              Util.IsEqual(plane.Normal.GetLength(), 1),
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
              Util.IsZero(plane.SignedDistanceTo(q)),
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

    public static class JtEdgeArrayExtensionMethods
    {
        /// <summary>
        /// Return a polygon as a list of XYZ points from
        /// an EdgeArray. If any of the edges are curved,
        /// we retrieve the tessellated points, i.e. an
        /// approximation determined by Revit.
        /// </summary>
        public static List<XYZ> GetPolygon(
          this EdgeArray ea)
        {
            int n = ea.Size;

            List<XYZ> polygon = new List<XYZ>(n);

            foreach (Edge e in ea)
            {
                IList<XYZ> pts = e.Tessellate();

                n = polygon.Count;

                if (0 < n)
                {
                    Debug.Assert(pts[0]
                      .IsAlmostEqualTo(polygon[n - 1]),
                      "expected last edge end point to "
                      + "equal next edge start point");

                    polygon.RemoveAt(n - 1);
                }
                polygon.AddRange(pts);
            }
            n = polygon.Count;

            Debug.Assert(polygon[0]
              .IsAlmostEqualTo(polygon[n - 1]),
              "expected first edge start point to "
              + "equal last edge end point");

            polygon.RemoveAt(n - 1);

            return polygon;
        }
    }

    public static class JtFamilyParameterExtensionMethods
    {
        public static bool IsShared(
          this FamilyParameter familyParameter)
        {
            MethodInfo mi = familyParameter
              .GetType()
              .GetMethod("getParameter",
                BindingFlags.Instance
                | BindingFlags.NonPublic);

            if (null == mi)
            {
                throw new InvalidOperationException(
                  "Could not find getParameter method");
            }

            var parameter = mi.Invoke(familyParameter,
              new object[] { }) as Parameter;

            return parameter.IsShared;
        }
    }

    public static class JtFilteredElementCollectorExtensions
    {
        public static FilteredElementCollector OfClass<T>(
          this FilteredElementCollector collector)
            where T : Element
        {
            return collector.OfClass(typeof(T));
        }

        public static IEnumerable<T> OfType<T>(
          this FilteredElementCollector collector)
            where T : Element
        {
            return Enumerable.OfType<T>(
              collector.OfClass<T>());
        }
    }

    public static class JtBuiltInCategoryExtensionMethods
    {
        /// <summary>
        /// Return a descriptive string for a built-in 
        /// category by removing the trailing plural 's' 
        /// and the OST_ prefix.
        /// </summary>
        public static string Description(
          this BuiltInCategory bic)
        {
            string s = bic.ToString().ToLower();
            s = s.Substring(4);
            Debug.Assert(s.EndsWith("s"), "expected plural suffix 's'");
            s = s.Substring(0, s.Length - 1);
            return s;
        }
    }

    public static class JtFamilyInstanceExtensionMethods
    {
        public static string GetColumnLocationMark(
          this FamilyInstance f)
        {
            Parameter p = f.get_Parameter(
              BuiltInParameter.COLUMN_LOCATION_MARK);

            return (p == null)
              ? string.Empty
              : p.AsString();
        }
    }
    #endregion // Extension Method Classes
}
