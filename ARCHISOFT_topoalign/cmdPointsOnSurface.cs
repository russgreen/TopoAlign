/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ARCHISOFT_topoalign
{
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
    [Transaction(TransactionMode.Manual)]
    public class cmdPointsOnSurface : IExternalCommand
    {
        private UIApplication uiapp;
        private UIDocument uidoc;
        private Autodesk.Revit.ApplicationServices.Application app;
        private Document doc;
        private Selection sel;
        private Util clsUtil = new Util();
        private Element m_Element;
        private Edge m_Edge;
        private Autodesk.Revit.DB.Architecture.TopographySurface m_TopoSurface;
        private View3D v3d;
        public Settings cSettings;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Crashes.GenerateTestCrash()

            cSettings = new Settings();
            cSettings.LoadSettings();
            uiapp = commandData.Application;
            uidoc = uiapp.ActiveUIDocument;
            app = uiapp.Application;
            doc = uidoc.Document;
            sel = uidoc.Selection;
            if (doc.ActiveView is View3D)
            {
                v3d = (View3D)doc.ActiveView;
            }
            else
            {
                TaskDialog.Show("Points on surface", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
                return Result.Failed;
            }

            var fh = new FailureHandler();
            var topoFilter = new TopoPickFilter();
            var lineFilter = new LinePickFilter();
            var elemFilter = new ElemPickFilter();
            IList<XYZ> points = new List<XYZ>();
            IList<XYZ> points1 = new List<XYZ>();

            // Dim xYZs1 As IList(Of Autodesk.Revit.DB.XYZ) = New List(Of Autodesk.Revit.DB.XYZ)()

            try
            {
                var refToposurface = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                m_TopoSurface = doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception ex)
            {
                return Result.Failed;
            }

            List<Curve> m_Curves;
            m_Curves = new List<Curve>();
            try
            {
                foreach (Reference r in uidoc.Selection.PickObjects(ObjectType.Element, lineFilter, "Select lines(s) to add topo points along"))
                {
                    Curve m_Curve = null;
                    ModelLine modelLine = doc.GetElement(r) as ModelLine;
                    ModelCurve modelCurve = doc.GetElement(r) as ModelCurve;
                    if (modelLine is object)
                    {
                        try
                        {
                            m_Curve = modelLine.GeometryCurve;
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (modelCurve is object)
                    {
                        try
                        {
                            m_Curve = modelCurve.GeometryCurve;
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (m_Curve is object)
                    {
                        m_Curves.Add(m_Curve);
                    }
                }
            }
            catch (Exception ex)
            {
                return Result.Failed;
            }

            // sort the curves and make contiguous
            // Dim creapp As Autodesk.Revit.Creation.Application = doc.Application.Create
            try
            {
                CurveUtils.SortCurvesContiguous(m_Curves);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Points on surface", "The lines selected must all be connected", TaskDialogCommonButtons.Ok);
                return Result.Failed;
            }

            bool CleanupTopoPoints = false;
            if (IsLoopClosed(m_Curves) == true)
            {
                if (TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.Yes) == TaskDialogResult.Yes)
                {
                    CleanupTopoPoints = true;
                }
            }

            // If = True Then
            // If TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes Or TaskDialogCommonButtons.No, TaskDialogResult.Yes) = TaskDialogResult.Ok Then
            // DeleteTopoPointsWithinCurves(m_Curves, m_TopoSurface)
            // End If
            // End If

            // If CurvesFormClosedPolygon(m_Curves) = True Then

            // End If

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Align topo"))
                {
                    tes.Start(m_TopoSurface.Id);
                    var opt = new Options();
                    opt.ComputeReferences = true;
                    points = GetPointsFromCurvesOnSurface(m_Curves);
                    if (points.Count == 0)
                    {
                        TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok);
                        tes.Cancel();
                        return Result.Failed;
                    }

                    // loop through each point and use reference intersector to get topo Z
                    foreach (XYZ pt in points)
                    {
                        // The 3 inputs for ReferenceIntersector are:
                        // A filter specifying that only ceilings will be reported
                        // A FindReferenceTarget option indicating what types of objects to report
                        // A 3d view (see http://wp.me/p2X0gy-2p for more info on how this works)

                        // Dim intersector As New ReferenceIntersector(New ElementCategoryFilter(BuiltInCategory.OST_TopographySurface), FindReferenceTarget.All, (From v In New FilteredElementCollector(doc).OfClass(GetType(View3D)).Cast(Of View3D)() Where v.IsTemplate = False AndAlso v.IsPerspective = False).First())
                        // Dim intersector As New ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, (From v In New FilteredElementCollector(doc).OfClass(GetType(View3D)).Cast(Of View3D)() Where v.IsTemplate = False AndAlso v.IsPerspective = False).First())
                        // Dim intersector As New ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, v3d)

                        // ' FindNearest finds the first item hit by the ray
                        // ' XYZ.BasisZ shoots the ray "up"
                        // Dim rwC As ReferenceWithContext = intersector.FindNearest(pt, XYZ.BasisZ)
                        // Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z + rwC.Proximity)

                        // points1.Add(pt1)

                        var startPt = pt;
                        var endPtUp = new XYZ(pt.X, pt.Y, pt.Z + 100d);
                        var dirUp = (endPtUp - startPt).Normalize();
                        var referenceIntersector = new ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, v3d);
                        var obstructionsOnUnboundLineUp = referenceIntersector.Find(startPt, dirUp);
                        XYZ point = null;
                        var gRefWithContext = obstructionsOnUnboundLineUp.FirstOrDefault();
                        var gRef = gRefWithContext.GetReference();
                        point = gRef.GlobalPoint;
                        points1.Add(point);
                    }

                    if (CleanupTopoPoints == true)
                    {
                        DeleteTopoPointsWithinCurves(m_Curves);
                    }

                    // we now have points with correct Z values.
                    // delete duplicate points and add to topo
                    // TODO: Check duplicates in more robust way.
                    // texting 0.1 instead of 0.01
                    var comparer = new XyzEqualityComparer(); // (0.1)
                    using (var t = new Transaction(doc, "add points"))
                    {
                        t.Start();
                        m_TopoSurface.AddPoints(points1.Distinct(comparer).ToList());
                        t.Commit();
                    }

                    tes.Commit(fh);
                }
            }
            catch (Exception ex)
            {
                return Result.Failed;
                return default;
            }

            return Result.Succeeded;
        }

        private IList<XYZ> GetPointsFromCurvesOnSurface(List<Curve> m_Curves)
        {
            var points = new List<XYZ>();
            double divide = 1000d * 0.00328084d;
            foreach (Curve m_Curve in m_Curves)
            {
                int i = m_Curve.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in m_Curve.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                        points.Add(pt1);
                    }
                }
                else
                {
                    double len = m_Curve.ApproximateLength;
                    if (len > divide)
                    {
                        var pt0 = new XYZ(m_Curve.Tessellate()[0].X, m_Curve.Tessellate()[0].Y, m_Curve.Tessellate()[0].Z);
                        var pt1 = new XYZ(m_Curve.Tessellate()[1].X, m_Curve.Tessellate()[1].Y, m_Curve.Tessellate()[1].Z);
                        foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, divide))
                        {
                            var p = new XYZ(pt.X, pt.Y, pt.Z);
                            points.Add(p);
                        }
                    }
                    else
                    {
                        foreach (XYZ pt in m_Curve.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                            points.Add(pt1);
                        }
                    }
                }
            }

            return points;
        }

        private bool IsLoopClosed(List<Curve> m_Curves)
        {
            if (CurveLoop.Create(m_Curves).IsOpen() == true)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void DeleteTopoPointsWithinCurves(List<Curve> m_Curves)
        {
            var polygons = new List<List<XYZ>>();
            foreach (Curve c in m_Curves)
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
            var min = new XYZ(bb.Min.X, bb.Min.Y, m_TopoSurface.get_BoundingBox(v).Min.Z);
            var max = new XYZ(bb.Max.X, bb.Max.Y, m_TopoSurface.get_BoundingBox(v).Max.Z);

            // Get topopoints withing bounding box 
            var outline = new Outline(min, max);
            var points = new List<XYZ>();
            var pts = new List<XYZ>();
            pts = m_TopoSurface.GetInteriorPoints() as List<XYZ>;
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
                    m_TopoSurface.DeletePoints(points1);
                    t.Commit();
                }
            }
        }
    }
}