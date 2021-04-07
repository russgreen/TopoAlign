using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class cmdPointsOnSurface : IExternalCommand
    {
        private UIApplication _uiapp;
        private UIDocument _uidoc;
        private Autodesk.Revit.ApplicationServices.Application _app;
        private Document _doc;
        private Selection _sel;
        private Util _util = new Util();
        private Element _element;
        private Edge _edge;
        private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
        private View3D _v3d;

        public Models.Settings cSettings;

        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            // Crashes.GenerateTestCrash()

            cSettings = new  Models.Settings();
            cSettings.LoadSettings();
            _uiapp = commandData.Application;
            _uidoc = _uiapp.ActiveUIDocument;
            _app = _uiapp.Application;
            _doc = _uidoc.Document;
            _sel = _uidoc.Selection;

            //check entitlement
            if (CheckEntitlement.LicenseCheck(_app) == false)
            {
                return Result.Cancelled;
            }

            //check the active view is a 3D view
            if (_doc.ActiveView is View3D)
            {
                _v3d = (View3D)_doc.ActiveView;
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
                var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception ex)
            {
                return Result.Failed;
            }

            List<Curve> curves;
            curves = new List<Curve>();
            try
            {
                foreach (Reference r in _uidoc.Selection.PickObjects(ObjectType.Element, lineFilter, "Select lines(s) to add topo points along"))
                {
                    Curve curve = null;
                    ModelLine modelLine = _doc.GetElement(r) as ModelLine;
                    ModelCurve modelCurve = _doc.GetElement(r) as ModelCurve;
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
                return Result.Failed;
            }

            // sort the curves and make contiguous
            try
            {
                CurveUtils.SortCurvesContiguous(curves);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Points on surface", "The lines selected must all be connected", TaskDialogCommonButtons.Ok);
                return Result.Failed;
            }

            bool CleanupTopoPoints = false;
            if (IsLoopClosed(curves) == true)
            {
                if (TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.Yes) == TaskDialogResult.Yes)
                {
                    CleanupTopoPoints = true;
                }
            }

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    var opt = new Options();
                    opt.ComputeReferences = true;
                    points = GetPointsFromCurvesOnSurface(curves);
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
                        var referenceIntersector = new ReferenceIntersector(_topoSurface.Id, FindReferenceTarget.All, _v3d);
                        var obstructionsOnUnboundLineUp = referenceIntersector.Find(startPt, dirUp);
                        XYZ point = null;
                        var gRefWithContext = obstructionsOnUnboundLineUp.FirstOrDefault();
                        var gRef = gRefWithContext.GetReference();
                        point = gRef.GlobalPoint;
                        points1.Add(point);
                    }

                    if (CleanupTopoPoints == true)
                    {
                        DeleteTopoPointsWithinCurves(curves);
                    }

                    // we now have points with correct Z values.
                    // delete duplicate points and add to topo
                    // TODO: Check duplicates in more robust way.
                    // testing 0.1 instead of 0.01
                    var comparer = new XyzEqualityComparer(); // (0.1)
                    using (var t = new Transaction(_doc, "add points"))
                    {
                        t.Start();
                        _topoSurface.AddPoints(points1.Distinct(comparer).ToList());
                        t.Commit();
                    }

                    tes.Commit(fh);
                }
            }
            catch (Exception ex)
            {
                return Result.Failed;
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

        private void DeleteTopoPointsWithinCurves(List<Curve> curves)
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
            var min = new XYZ(bb.Min.X, bb.Min.Y, _topoSurface.get_BoundingBox(v).Min.Z);
            var max = new XYZ(bb.Max.X, bb.Max.Y, _topoSurface.get_BoundingBox(v).Max.Z);

            // Get topopoints withing bounding box 
            var outline = new Outline(min, max);
            var points = new List<XYZ>();
            var pts = new List<XYZ>();
            pts = _topoSurface.GetInteriorPoints() as List<XYZ>;
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
                using (var t = new Transaction(_doc, "removing points"))
                {
                    t.Start();
                    _topoSurface.DeletePoints(points1);
                    t.Commit();
                }
            }
        }
    }
}
