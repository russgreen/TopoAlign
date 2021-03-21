/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.VisualBasic;

namespace ARCHISOFT_topoalign
{
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */


    public class Util
    {
        public const double MAX_ROUNDING_PRECISION = 0.000000000001d;

        public void ShowBasicElementInfo(ref Document m_rvtDoc, Element elem)
        {

            // '  let's see what kind of element we got. 
            string s = "You picked:" + Constants.vbCr;
            s = s + "  Class name = " + elem.GetType().Name + Constants.vbCr;
            s = s + "  Category = " + elem.Category.Name + Constants.vbCr;
            s = s + "  Element id = " + elem.Id.ToString() + Constants.vbCr + Constants.vbCr;

            // '  and check its type info. 
            var elemTypeId = elem.GetTypeId();
            ElementType elemType = m_rvtDoc.GetElement(elemTypeId) as ElementType;
            s = s + "Its ElementType:" + Constants.vbCr;
            s = s + "  Class name = " + elemType.GetType().Name + Constants.vbCr;
            s = s + "  Category = " + elemType.Category.Name + Constants.vbCr;
            s = s + "  Element type id = " + elemType.Id.ToString() + Constants.vbCr;

            // '  show what we got. 
            TaskDialog.Show("Basic Element Info", s);
        }

        /* TODO ERROR: Skipped RegionDirectiveTrivia */    /// <summary>
    /// Base units currently used internally by Revit.
    /// </summary>
        private enum BaseUnit
        {
            BU_Length = 0,           // length, feet (ft)
            BU_Angle,                // angle, radian (rad)
            BU_Mass,                 // mass, kilogram (kg)
            BU_Time,                 // time, second (s)
            BU_Electric_Current,     // electric current, ampere (A)
            BU_Temperature,          // temperature, kelvin (K)
            BU_Luminous_Intensity,   // luminous intensity, candela (cd)
            BU_Solid_Angle,          // solid angle, steradian (sr)
            NumBaseUnits
        }

        private const double _convertFootToMm = 12d * 25.4d;
        private const double _convertFootToMeter = _convertFootToMm * 0.001d;
        private const double _convertCubicFootToCubicMeter = _convertFootToMeter * _convertFootToMeter * _convertFootToMeter;

        /// <summary>
    /// Convert a given length in feet to millimetres.
    /// </summary>
        public static double FootToMm(double length)
        {
            return length * _convertFootToMm;
        }

        /// <summary>
    /// Convert a given length in millimetres to feet.
    /// </summary>
        public static double MmToFoot(double length)
        {
            return length / _convertFootToMm;
        }

        /// <summary>
    /// Convert a given point or vector from millimetres to feet.
    /// </summary>
        public static XYZ MmToFoot(XYZ v)
        {
            return v.Divide(_convertFootToMm);
        }

        /// <summary>
    /// Convert a given volume in feet to cubic meters.
    /// </summary>
        public static double CubicFootToCubicMeter(double volume)
        {
            return volume * _convertCubicFootToCubicMeter;
        }

        // Public Shared Function getCurrentDisplayUnitType(unitType As UnitType, document As Document, ByRef roundingPrecision As System.Nullable(Of Double)) As DisplayUnitType

        // ' Following good SOA practices, don't trust the incoming parameters and verify
        // ' that they have values that can be worked with before doing anything.
        // If document Is Nothing Then
        // Throw New ArgumentNullException("document")
        // End If

        // ' This function does not require the document to be a family document, so don't check for that.

        // Dim formatOption As FormatOptions
        // Dim result As DisplayUnitType

        // Dim projectUnit As Units = document.GetUnits

        // Try

        // formatOption = projectUnit.GetFormatOptions(unitType)
        // Catch ex As Exception
        // Throw New ArgumentOutOfRangeException("The UnitType '" + unitType.ToString() + "' does not have any DisplayUnitType options.", ex)
        // End Try

        // Try
        // result = formatOption.UnitSymbol
        // Catch ex As Exception
        // Throw New Exception("Unable to get the DisplayUnitType for UnitType '" + unitType.ToString() + "'", ex)
        // End Try


        // Try
        // roundingPrecision = formatOption.GetRounding
        // Catch generatedExceptionName As Exception
        // ' Not all dimensional types support rounding, so if an exception is thrown,
        // ' store the fact into our nullable double that there is no value.
        // roundingPrecision = Nothing
        // End Try


        // Return result
        // End Function

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        private const double _eps = 0.000000001d;

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

        public static bool IsZero(double a, double tolerance)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsZero(double a)
        {
            return IsZero(a, _eps);
        }

        public static bool IsEqual(double a, double b)
        {
            return IsZero(b - a);
        }

        public static int Compare(double a, double b)
        {
            return IsEqual(a, b) ? 0 : a < b ? -1 : 1;
        }

        public static int Compare(XYZ p, XYZ q)
        {
            int d = Compare(p.X, q.X);
            if (0 == d)
            {
                d = Compare(p.Y, q.Y);
                if (0 == d)
                {
                    d = Compare(p.Z, q.Z);
                }
            }

            return d;
        }

        public static bool IsEqual(XYZ p, XYZ q)
        {
            return 0 == Compare(p, q);
        }

        /// <summary>
    /// Return true if the vectors v and w
    /// are non-zero and perpendicular.
    /// </summary>
        private bool IsPerpendicular(XYZ v, XYZ w)
        {
            double a = v.GetLength();
            double b = v.GetLength();
            double c = Math.Abs(v.DotProduct(w));
            return _eps < a && _eps < b && _eps > c;
            // c * c < _eps * a * b
        }

        public static bool IsParallel(XYZ p, XYZ q)
        {
            return p.CrossProduct(q).IsZeroLength();
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
            return IsZero(v.X, tolerance) && IsZero(v.Y, tolerance);
        }

        public static bool IsHorizontal(Edge e)
        {
            var p = e.Evaluate(0d);
            var q = e.Evaluate(1d);
            return IsHorizontal(q - p);
        }

        // Public Shared Function IsHorizontal(f As PlanarFace) As Boolean
        // 'Return IsVertical(f.Normal) '2015
        // Return IsVertical(f.FaceNormal) '2016
        // End Function

        // Public Shared Function IsVertical(f As PlanarFace) As Boolean
        // 'Return IsHorizontal(f.Normal)'2015
        // Return IsHorizontal(f.FaceNormal) '2016
        // End Function

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
        private const double _minimumSlope = 0.3d;

        /// <summary>
    /// Return true if the Z coordinate of the
    /// given vector is positive and the slope
    /// is larger than the minimum limit.
    /// </summary>
        public static bool PointsUpwards(XYZ v)
        {
            double horizontalLength = v.X * v.X + v.Y * v.Y;
            double verticalLength = v.Z * v.Z;
            return 0d < v.Z && _minimumSlope < verticalLength / horizontalLength;

            // return _eps < v.Normalize().Z;
            // return _eps < v.Normalize().Z && IsVertical( v.Normalize(), tolerance );
        }

        public static bool PointsDownwards(XYZ v)
        {
            double horizontalLength = v.X * v.X + v.Y * v.Y;
            double verticalLength = v.Z * v.Z;
            return 0d > v.Z && _minimumSlope < verticalLength / horizontalLength;

            // return _eps < v.Normalize().Z;
            // return _eps < v.Normalize().Z && IsVertical( v.Normalize(), tolerance );
        }

        /// <summary>
    /// Return true if the given bounding box bb
    /// contains the given point p in its interior.
    /// </summary>
        public bool BoundingBoxXyzContains(BoundingBoxXYZ bb, XYZ p)
        {
            bool retval = 0 < Compare(bb.Min, p) && 0 < Compare(p, bb.Max);
            return retval;
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */    
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
        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */    
        /// <summary>
    /// Return the midpoint between two points.
    /// </summary>
        public static XYZ Midpoint(XYZ p, XYZ q)
        {
            return p + 0.5d * (q - p);
        }

        /// <summary>
    /// Return the midpoint of a Line.
    /// </summary>
        public static XYZ Midpoint(Line line)
        {
            return Midpoint(line.GetEndPoint(0), line.GetEndPoint(1));
        }

        /// <summary>
    /// Return the normal of a Line in the XY plane.
    /// </summary>
        public static XYZ Normal(Line line)
        {
            var p = line.GetEndPoint(0);
            var q = line.GetEndPoint(1);
            var v = q - p;

            // Debug.Assert( IsZero( v.Z ),
            // "expected horizontal line" );

            return v.CrossProduct(XYZ.BasisZ).Normalize();
        }

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
    /// Return the four XYZ corners of the given
    /// bounding box in the XY plane at the minimum
    /// Z elevation in the order lower left, lower
    /// right, upper right, upper left:
    /// </summary>
        // Public Shared Function GetCorners(b As BoundingBoxXYZ) As XYZ()
        // Dim z As Double = AddressOf b.Min.Z

        // Return New XYZ() {New XYZ(AddressOf b.Min.X, AddressOf b.Min.Y, z), New XYZ(AddressOf b.Max.X, AddressOf b.Min.Y, z), New XYZ(AddressOf b.Max.X, AddressOf b.Max.Y, z), New XYZ(AddressOf b.Min.X, AddressOf b.Max.Y, z)}
        // End Function

        /// <summary>
    /// Offset the generated boundary polygon loop
    /// model lines downwards to separate them from
    /// the slab edge.
    /// </summary>
        private const double _offset = 0.1d;

        /// <summary>
    /// Determine the boundary polygons of the lowest
    /// horizontal planar face of the given solid.
    /// </summary>
    /// <param name="polygons">Return polygonal boundary
    /// loops of lowest horizontal face, i.e. profile of
    /// circumference and holes</param>
    /// <param name="solid">Input solid</param>
    /// <returns>False if no horizontal planar face was
    /// found, else true</returns>
        private static bool GetBoundary(List<List<XYZ>> polygons, Solid solid)
        {
            PlanarFace lowest = null;
            var faces = solid.Faces;
            foreach (Face f in faces)
            {
                PlanarFace pf = f as PlanarFace;
                if (pf is object && IsBottomFace(f) == true)
                {
                    if (lowest is null || pf.Origin.Z < lowest.Origin.Z)
                    {
                        lowest = pf;
                        XYZ p;
                        var q = XYZ.Zero;
                        bool first;
                        int i;
                        int n;
                        var loops = lowest.EdgeLoops;
                        foreach (EdgeArray loop in loops)
                        {
                            var vertices = new List<XYZ>();
                            first = true;
                            foreach (Edge e in loop)
                            {
                                var points = e.Tessellate();
                                p = points[0];
                                if (!first)
                                {
                                    Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" + " to equal previous end point");
                                }

                                n = points.Count;
                                q = points[n - 1];
                                var loopTo = n - 2;
                                for (i = 0; i <= loopTo; i++)
                                {
                                    var v = points[i];
                                    v -= _offset * XYZ.BasisZ;
                                    vertices.Add(v);
                                }
                            }

                            q -= _offset * XYZ.BasisZ;
                            Debug.Assert(q.IsAlmostEqualTo(vertices[0]), "expected last end point to equal" + " first start point");
                            polygons.Add(vertices);
                        }
                    }
                }
            }
            // If lowest IsNot Nothing Then
            // Dim p As XYZ, q As XYZ = XYZ.Zero
            // Dim first As Boolean
            // Dim i As Integer, n As Integer
            // Dim loops As EdgeArrayArray = lowest.EdgeLoops
            // For Each [loop] As EdgeArray In loops
            // Dim vertices As New List(Of XYZ)()
            // first = True
            // For Each e As Edge In [loop]
            // Dim points As IList(Of XYZ) = e.Tessellate()
            // p = points(0)
            // If Not first Then
            // Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" & " to equal previous end point")
            // End If
            // n = points.Count
            // q = points(n - 1)
            // For i = 0 To n - 2
            // Dim v As XYZ = points(i)
            // v -= _offset * XYZ.BasisZ
            // vertices.Add(v)
            // Next
            // Next
            // q -= _offset * XYZ.BasisZ
            // Debug.Assert(q.IsAlmostEqualTo(vertices(0)), "expected last end point to equal" & " first start point")
            // polygons.Add(vertices)
            // Next
            // End If
            return lowest is object;
        }

        /// <summary>
    /// Return all floor slab boundary loop polygons
    /// for the given floors, offset downwards from the
    /// bottom floor faces by a certain amount.
    /// </summary>
        public static List<List<XYZ>> GetBoundaryPolygons(List<Element> elems, Options opt)
        {
            var polygons = new List<List<XYZ>>();
            foreach (Element e in elems)
            {
                var geo = e.get_Geometry(opt);

                // GeometryObjectArray objects = geo.Objects; // 2012
                // foreach( GeometryObject obj in objects ) // 2012

                foreach (GeometryObject obj in geo)
                {
                    // 2013
                    Solid solid = obj as Solid;
                    if (solid is object)
                    {
                        GetBoundary(polygons, solid);
                    }
                }
            }

            return polygons;
        }

        /// <summary>
    /// Return the 2D intersection point between two
    /// unbounded lines defined In the XY plane by the
    /// start And end points of the two given curves.
    /// By Magson Leone.
    /// Return null If the two lines are coincident,
    /// in which case the intersection Is an infinite
    /// line, Or non-coincident And parallel, in which
    /// Case it Is empty.
    /// https : //en.wikipedia.org/wiki/Line%E2%80%93line_intersection
    /// </summary>
        public static XYZ Intersection(Curve c1, Curve c2)
        {
            var p1 = c1.GetEndPoint(0);
            var q1 = c1.GetEndPoint(1);
            var p2 = c2.GetEndPoint(0);
            var q2 = c2.GetEndPoint(1);
            var v1 = q1 - p1;
            var v2 = q2 - p2;
            var w = p2 - p1;
            XYZ p5 = null;
            double c = (v2.X * w.Y - v2.Y * w.X) / (v2.X * v1.Y - v2.Y * v1.X);
            if (!double.IsInfinity(c))
            {
                double x = p1.X + c * v1.X;
                double y = p1.Y + c * v1.Y;
                p5 = new XYZ(x, y, 0d);
            }

            return p5;
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        public Parameter FindParameterByName(ref Element element, string Definition)
        {
            Parameter foundParameter = null;
            // This will find the first parameter that measures length
            foreach (Parameter parameter in element.Parameters)
            {
                if ((parameter.Definition.Name ?? "") == (Definition ?? ""))
                {
                    foundParameter = parameter;
                    break;
                }
            }

            return foundParameter;
        }

        /* TODO ERROR: Skipped RegionDirectiveTrivia */    /// <summary>
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
    /// Return a string for a real number
    /// formatted to two decimal places.
    /// </summary>
        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }


        /// <summary>
    /// Return a string for a UV point
    /// or vector with its coordinates
    /// formatted to two decimal places.
    /// </summary>
        public static string PointString(UV p)
        {
            return string.Format("({0},{1})", RealString(p.U), RealString(p.V));
        }

        /// <summary>
    /// Return a string for an XYZ point
    /// or vector with its coordinates
    /// formatted to two decimal places.
    /// </summary>
        public static string PointString(XYZ p)
        {
            return string.Format("({0},{1},{2})", RealString(p.X), RealString(p.Y), RealString(p.Z));
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        // Set values specific to the environment 
        public const string _baseApiUrl = "https://apps.exchange.autodesk.com/";

        [Serializable]
        public class EntitlementResponse
        {
            public string UserId
            {
                get
                {
                    return m_UserId;
                }

                set
                {
                    m_UserId = value;
                }
            }

            private string m_UserId;

            public string AppId
            {
                get
                {
                    return m_AppId;
                }

                set
                {
                    m_AppId = value;
                }
            }

            private string m_AppId;

            public bool IsValid
            {
                get
                {
                    return m_IsValid;
                }

                set
                {
                    m_IsValid = value;
                }
            }

            private bool m_IsValid;

            public string Message
            {
                get
                {
                    return m_Message;
                }

                set
                {
                    m_Message = value;
                }
            }

            private string m_Message;
        }
    }
}