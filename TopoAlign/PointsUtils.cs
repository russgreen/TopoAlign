using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
//using GeometRi;
using g3;

namespace TopoAlign
{
    public class PointsUtils
    {
        private static View3D _v3d;

        public static bool PointsAlongLines(
            UIDocument uidoc, 
            Document doc, 
            Autodesk.Revit.DB.Architecture.TopographySurface topoSurface,
            double divide, 
            double offset = 0)
        {
            //check the active view is a 3D view
            if (doc.ActiveView is View3D)
            {
                _v3d = (View3D)doc.ActiveView;
            }
            else
            {
                TaskDialog.Show("Points along lines", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
                return false;
            }

            var fh = new FailureHandler();
            var topoFilter = new TopoPickFilter();
            var lineFilter = new LinePickFilter();
            var elemFilter = new ElemPickFilter();
            IList<XYZ> points = new List<XYZ>();
            IList<XYZ> points1 = new List<XYZ>();

            try
            {
                var refToposurface = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                topoSurface = doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception ex)
            {
                return false;
            }

            List<Curve> curves;
            curves = new List<Curve>();
            try
            {
                foreach (Reference r in uidoc.Selection.PickObjects(ObjectType.Element, lineFilter, "Select lines(s) to add topo points along"))
                {
                    Curve curve = null;
                    ModelLine modelLine = doc.GetElement(r) as ModelLine;
                    ModelCurve modelCurve = doc.GetElement(r) as ModelCurve;
                    if (modelLine is object)
                    {
                        try
                        {
                            curve = modelLine.GeometryCurve;
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (modelCurve is object)
                    {
                        try
                        {
                            curve = modelCurve.GeometryCurve;
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (curve is object)
                    {
                        curves.Add(curve);
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            // sort the curves and make contiguous
            try
            {
                CurveUtils.SortCurvesContiguous(curves);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Points along lines", "The lines selected must all be connected. ", TaskDialogCommonButtons.Ok);
                return false;
            }

            bool CleanupTopoPoints = false;
            if (IsLoopClosed(curves) == true)
            {
                if (TaskDialog.Show("Points along lines", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.Yes) == TaskDialogResult.Yes)
                {
                    CleanupTopoPoints = true;
                }
            }

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Align topo"))
                {
                    tes.Start(topoSurface.Id);
                    var opt = new Options();
                    opt.ComputeReferences = true;
                    points = PointsUtils.GetPointsFromCurves(curves, divide, offset);

                    if (points.Count == 0)
                    {
                        TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok);
                        tes.Cancel();
                        return false;
                    }

                    if (CleanupTopoPoints == true)
                    {
                        DeleteTopoPointsWithinCurves(curves, doc,topoSurface);
                    }

                    // we now have points with correct Z values.
                    // delete duplicate points and add to topo
                    // TODO: Check duplicates in more robust way.
                    // testing 0.1 instead of 0.01
                    var comparer = new XyzEqualityComparer(); // (0.1)
                    using (var t = new Transaction(doc, "add points"))
                    {
                        t.Start();
                        topoSurface.AddPoints(points.Distinct(comparer).ToList());
                        t.Commit();
                    }

                    tes.Commit(fh);
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }


        public static IList<XYZ> GetPointsFromCurves(List<Curve> curves, double divide = 1000d * 0.00328084d, double offset = 0)
        {
            var points = new List<XYZ>();

            foreach (var curve in curves)
            {
                int i = curve.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in curve.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z + offset);
                        points.Add(pt1);
                    }
                }
                else
                {
                    double len = curve.ApproximateLength;
                    if (len > divide)
                    {
                        var pt0 = new XYZ(curve.Tessellate()[0].X, curve.Tessellate()[0].Y, curve.Tessellate()[0].Z);
                        var pt1 = new XYZ(curve.Tessellate()[1].X, curve.Tessellate()[1].Y, curve.Tessellate()[1].Z);
                        foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, divide))
                        {
                            var p = new XYZ(pt.X, pt.Y, pt.Z + offset);
                            points.Add(p);
                        }
                    }
                    else
                    {
                        foreach (XYZ pt in curve.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z + offset);
                            points.Add(pt1);
                        }
                    }
                }
            }

            return points;
        }
        public static List<XYZ> GetPointsFromVector3ds(List<Vector3d> Vector3ds, double offset = 0)
        {
            var points = new List<XYZ>();

            foreach (var vector in Vector3ds)
            {
                var xyz = new XYZ(vector.x, vector.y, vector.z + offset);

                points.Add(xyz);
            }

            return points;
        }


        public static bool IsLoopClosed(List<Curve> curves)
        {
            if (CurveLoop.Create(curves).IsOpen() == true)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static void DeleteTopoPointsWithinCurves(List<Curve> curves, Document doc, Autodesk.Revit.DB.Architecture.TopographySurface topoSurface)
        {
            var polygons = new List<List<XYZ>>();
            foreach (Curve c in curves)
            {
                var polygon = new List<XYZ>();
                int i = c.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in c.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                        polygon.Add(pt1);
                    }
                }
                else
                {
                    foreach (XYZ pt in c.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                        polygon.Add(pt1);
                    }
                }

                polygons.Add(polygon);
            }

            var flat_polygons = Util.Flatten(polygons);
            var xyzs = new List<XYZ>();
            foreach (List<XYZ> polygon in polygons)
            {
                foreach (XYZ pt in polygon)
                    xyzs.Add(pt);
            }

            var uvArr = new UVArray(xyzs);
            var poly = new List<UV>();
            foreach (List<UV> polygon in flat_polygons)
            {
                foreach (UV pt in polygon)
                    poly.Add(pt);
            }

            // bounding box of curves and topo for elevation
            var bb = new JtBoundingBoxXyz();
            bb = JtBoundingBoxXyz.GetBoundingBoxOf(polygons);
            Autodesk.Revit.DB.View v = null;
            var min = new XYZ(bb.Min.X, bb.Min.Y, topoSurface.get_BoundingBox(v).Min.Z);
            var max = new XYZ(bb.Max.X, bb.Max.Y, topoSurface.get_BoundingBox(v).Max.Z);

            // Get topopoints withing bounding box 
            var outline = new Outline(min, max);
            var points = new List<XYZ>();
            var pts = new List<XYZ>();
            pts = topoSurface.GetInteriorPoints() as List<XYZ>;
            foreach (XYZ pt in pts)
            {
                if (outline.Contains(pt, 0.000000001d))
                {
                    points.Add(pt);
                }
            }

            // Check each point to see if point is with 2D boundary
            var points1 = new List<XYZ>();
            using (var pf = new ProgressForm("Analyzing topo points.", "{0} points of " + points.Count + " processed...", points.Count))
            {
                foreach (XYZ pt in points)
                {
                    // If PointInPoly.PolygonContains(poly, Util.Flatten(pt)) = True Then
                    // If PointInPoly.PolygonContains(uvArr, Util.Flatten(pt)) = True Then
                    if (PointInPoly.PointInPolygon(uvArr, Util.Flatten(pt)) == true)
                    {
                        points1.Add(pt);
                    }

                    pf.Increment();
                }
            }

            // Remove topo points if answer is true
            if (points1.Count > 0)
            {
                using (var t = new Transaction(doc, "removing points"))
                {
                    t.Start();
                    topoSurface.DeletePoints(points1);
                    t.Commit();
                }
            }
        }
    }
}
