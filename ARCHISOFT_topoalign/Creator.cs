/* TODO ERROR: Skipped RegionDirectiveTrivia */// 
                                               // Creator.cs - model line creator helper class
                                               // 
                                               // Copyright (C) 2008-2013 by Jeremy Tammik,
                                               // Autodesk Inc. All rights reserved.
                                               // 
/* TODO ERROR: Skipped EndRegionDirectiveTrivia */
/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;

namespace ARCHISOFT_topoalign
{
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
    class Creator
    {
        private Document _doc;

        // these are
        // Autodesk.Revit.Creation
        // objects!
        private Autodesk.Revit.Creation.Application _creapp;
        private Autodesk.Revit.Creation.Document _credoc;

        public Creator(Document doc)
        {
            _doc = doc;
            _credoc = doc.Create;
            _creapp = doc.Application.Create;
        }

        /// <summary>
    /// Determine the plane that a given curve resides in and return its normal vector.
    /// Ask the curve for its start and end points and some curve in the middle.
    /// The latter can be obtained by asking the curve for its parameter range and
    /// evaluating it in the middle, or by tessellation. In case of tessellation,
    /// you could iterate through the tessellation points and use each one together
    /// with the start and end points to try and determine a valid plane.
    /// Once one is found, you can add debug assertions to ensure that the other
    /// tessellation points (if there are any more) are in the same plane.
    /// In the case of the line, the tessellation only returns two points.
    /// I once heard that that is the only element that can do that, all
    /// non-linear curves return at least three. So you could use this property
    /// to determine that a line is a line (and add an assertion as well, if you like).
    /// Update, later: please note that the Revit API provides an overload of the
    /// NewPlane method taking a CurveArray argument.
    /// </summary>
        private XYZ GetCurveNormal(Curve curve)
        {
            var pts = curve.Tessellate();
            int n = pts.Count;
            Debug.Assert(1 < n, "expected at least two points " + "from curve tessellation");
            var p = pts[0];
            var q = pts[n - 1];
            var v = q - p;
            XYZ w;
            XYZ normal = null;
            if (2 == n)
            {
                Debug.Assert(curve is Line, "expected non-line element to have " + "more than two tessellation points");

                // for non-vertical lines, use Z axis to
                // span the plane, otherwise Y axis:

                double dxy = Math.Abs(v.X) + Math.Abs(v.Y);
                w = dxy > Util.TolPointOnPlane ? XYZ.BasisZ : XYZ.BasisY;
                normal = v.CrossProduct(w).Normalize();
            }
            else
            {
                int i = 0;
                while (System.Threading.Interlocked.Increment(ref i) < n - 1)
                {
                    w = pts[i] - p;
                    normal = v.CrossProduct(w);
                    if (!normal.IsZeroLength())
                    {
                        normal = normal.Normalize();
                        break;
                    }
                }

                // #If DEBUG Then
                // If True Then
                // Dim normal2 As XYZ
                // While System.Threading.Interlocked.Increment(i) < n - 1
                // w = pts(i) - p
                // normal2 = v.CrossProduct(w)
                // Debug.Assert(normal2.IsZeroLength() OrElse Util.IsZero(normal2.AngleTo(normal)), "expected all points of curve to " & "lie in same plane")
                // End While
                // #End If

            }

            return normal;
        }

        /// <summary>
    /// Miroslav Schonauer's model line creation method.
    /// A utility function to create an arbitrary sketch
    /// plane given the model line end points.
    /// </summary>
    /// <param name="app">Revit application</param>
    /// <param name="p">Model line start point</param>
    /// <param name="q">Model line end point</param>
    /// <returns></returns>
        public static ModelLine CreateModelLine(Document doc, XYZ p, XYZ q)
        {
            if (p.DistanceTo(q) < Util.MinLineLength)
            {
                return null;
            }

            // Create sketch plane; for non-vertical lines,
            // use Z-axis to span the plane, otherwise Y-axis:

            var v = q - p;
            double dxy = Math.Abs(v.X) + Math.Abs(v.Y);
            var w = dxy > Util.TolPointOnPlane ? XYZ.BasisZ : XYZ.BasisY;
            var norm = v.CrossProduct(w).Normalize();
            var creApp = doc.Application.Create;

            /* TODO ERROR: Skipped IfDirectiveTrivia */
            var plane = Plane.CreateByNormalAndOrigin(norm, p);
            /* TODO ERROR: Skipped ElseDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */

            var creDoc = doc.Create;

            // SketchPlane sketchPlane = creDoc.NewSketchPlane( plane ); // 2013
            var sketchPlane__1 = SketchPlane.Create(doc, plane);
            // 2014
            // creApp.NewLine( p, q, true ), // 2013
            // 2014
            return creDoc.NewModelCurve(Line.CreateBound(p, q), sketchPlane__1) as ModelLine;
        }

        private SketchPlane NewSketchPlanePassLine(Line line)
        {
            var p = line.GetEndPoint(0);
            var q = line.GetEndPoint(1);
            XYZ norm;
            if (p.X == q.X)
            {
                norm = XYZ.BasisX;
            }
            else if (p.Y == q.Y)
            {
                norm = XYZ.BasisY;
            }
            else
            {
                norm = XYZ.BasisZ;
            }
            /* TODO ERROR: Skipped IfDirectiveTrivia */
            var plane = Plane.CreateByNormalAndOrigin(norm, p);
            /* TODO ERROR: Skipped ElseDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */
            // return _credoc.NewSketchPlane( plane ); // 2013

            return SketchPlane.Create(_doc, plane);
            // 2014
        }

        public void CreateModelLine(XYZ p, XYZ q)
        {
            if (p.IsAlmostEqualTo(q))
            {
                throw new ArgumentException("Expected two different points.");
            }

            var line__1 = Line.CreateBound(p, q);
            if (line__1 is null)
            {
                throw new Exception("Geometry line creation failed.");
            }

            _credoc.NewModelCurve(line__1, NewSketchPlanePassLine(line__1));
        }

        /// <summary>
    /// Return a new sketch plane containing the given curve.
    /// Update, later: please note that the Revit API provides
    /// an overload of the NewPlane method taking a CurveArray
    /// argument, which could presumably be used instead.
    /// </summary>
        private SketchPlane NewSketchPlaneContainCurve(Curve curve)
        {
            var p = curve.GetEndPoint(0);
            var normal = GetCurveNormal(curve);
            /* TODO ERROR: Skipped IfDirectiveTrivia */
            var plane = Plane.CreateByNormalAndOrigin(normal, p);
            /* TODO ERROR: Skipped ElseDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */
            // #If DEBUG Then
            // If Not (TypeOf curve Is Line) Then
            // Dim a As CurveArray = _creapp.NewCurveArray()
            // a.Append(curve)
            // Dim plane2 As Plane = _creapp.NewPlane(a)

            // Debug.Assert(Util.IsParallel(plane2.Normal, plane.Normal), "expected equal planes")

            // Debug.Assert(Util.IsZero(plane2.SignedDistanceTo(plane.Origin)), "expected equal planes")
            // End If
            // #End If

            // return _credoc.NewSketchPlane( plane ); // 2013

            return SketchPlane.Create(_doc, plane);
            // 2014
        }

        public void CreateModelCurve(Curve curve)
        {
            _credoc.NewModelCurve(curve, NewSketchPlaneContainCurve(curve));
        }

        public void DrawPolygons(List<List<XYZ>> loops)
        {
            var p1 = XYZ.Zero;
            var q = XYZ.Zero;
            bool first;
            foreach (List<XYZ> loop in loops)
            {
                first = true;
                foreach (XYZ p in loop)
                {
                    if (first)
                    {
                        p1 = p;
                        first = false;
                    }
                    else
                    {
                        CreateModelLine(p, q);
                    }

                    q = p;
                }

                CreateModelLine(q, p1);
            }
        }

        public void DrawFaceTriangleNormals(Face f)
        {
            var mesh = f.Triangulate();
            int n = mesh.NumTriangles;
            string s = "{0} face triangulation returns " + "mesh triangle{1} and normal vector{1}:";
            Debug.Print(s, n, Util.PluralSuffix(n));
            for (int i = 0, loopTo = n - 1; i <= loopTo; i++)
            {
                var t = mesh.get_Triangle(i);
                var p = (t.get_Vertex(0) + t.get_Vertex(1) + t.get_Vertex(2)) / 3d;
                var v = t.get_Vertex(1) - t.get_Vertex(0);
                var w = t.get_Vertex(2) - t.get_Vertex(0);
                var normal = v.CrossProduct(w).Normalize();
                Debug.Print("{0} {1} --> {2}", i, Util.PointString(p), Util.PointString(normal));
                CreateModelLine(p, p + normal);
            }
        }
    }
}