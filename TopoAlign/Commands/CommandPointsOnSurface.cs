using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TopoAlign.Comparers;
using TopoAlign.Geometry;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandPointsOnSurface : IExternalCommand
{
    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Document _doc;
    private Selection _sel;
    private decimal _offset;
    private decimal _divide;
    private Element _element;
    private Edge _edge;
    private View3D _v3d;
    private Units _docUnits;

#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif

#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits; 
    private DisplayUnitType _useDisplayUnits;
#else
    private ForgeTypeId _docDisplayUnits;
    private ForgeTypeId _useDisplayUnits;
#endif

    public Models.Settings cSettings;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        cSettings = new Models.Settings();
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

#if REVIT2018 || REVIT2019 || REVIT2020
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif
        using (FormDivideLines frm = new FormDivideLines())
        {
            _divide = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)cSettings.DivideEdgeDistance, _docDisplayUnits));

            frm.nudVertOffset.Value = 0;
            frm.nudVertOffset.Enabled = false;

            if (_divide > frm.nudDivide.Maximum)
            {
                frm.nudDivide.Value = frm.nudDivide.Maximum;
            }
            else
            {
                frm.nudDivide.Value = _divide;
            }

#if REVIT2018 || REVIT2019 || REVIT2020
            foreach (DisplayUnitType displayUnitType in UnitUtils.GetValidDisplayUnits(UnitType.UT_Length))
            {
                frm.DisplayUnitTypecomboBox.Items.AddRange(new object[] { displayUnitType });
                frm.DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelFor(displayUnitType));
            }
#else
            foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
            {
                frm.DisplayUnitTypecomboBox.Items.AddRange(new object[] { displayUnitType });
                frm.DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelForUnit(displayUnitType));
            }
#endif
            frm.DisplayUnitTypecomboBox.SelectedItem = _docDisplayUnits;
            frm.DisplayUnitcomboBox.SelectedIndex = frm.DisplayUnitTypecomboBox.SelectedIndex;

            if (frm.ShowDialog() == DialogResult.OK)
            {
#if REVIT2018 || REVIT2019 || REVIT2020
                _useDisplayUnits = (DisplayUnitType)frm.DisplayUnitTypecomboBox.SelectedItem;
                _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
#else
                _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
#endif

                //first save the settings for next time
                cSettings.DivideEdgeDistance = _divide;
                cSettings.SaveSettings();

                if (PointsOnSurfaceAlongLines() == false)
                {
                    return Result.Failed;
                }
            }
        }

        // Return Success
        return Result.Succeeded;
    }

    private bool PointsOnSurfaceAlongLines()
    {
        //check the active view is a 3D view
        if (_doc.ActiveView is View3D)
        {
            _v3d = (View3D)_doc.ActiveView;
        }
        else
        {
            TaskDialog.Show("Points on surface", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
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
            var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");

#if REVIT2024_OR_GREATER
            _topoSolid = _doc.GetElement(refToposurface) as Toposolid;
#else
            _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
#endif
        }
        catch (Exception)
        {
            return false;
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
                    catch (Exception)
                    {
                    }
                }

                if (modelCurve is object)
                {
                    try
                    {
                        curve = modelCurve.GeometryCurve;
                    }
                    catch (Exception)
                    {
                    }
                }

                if (curve is object)
                {
                    curves.Add(curve);
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        // sort the curves and make contiguous
        try
        {
            CurveUtils.SortCurvesContiguous(curves);
        }
        catch (Exception)
        {
            TaskDialog.Show("Points on surface", "The lines selected must all be connected", TaskDialogCommonButtons.Ok);
            return false;
        }

        bool CleanupTopoPoints = false;
        if (PointsUtils.IsLoopClosed(curves) == true)
        {
            if (TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.Yes) == TaskDialogResult.Yes)
            {
                CleanupTopoPoints = true;
            }
        }

        try
        {

#if REVIT2024_OR_GREATER

            var opt = new Options();
            opt.ComputeReferences = true;
            points = PointsUtils.GetPointsFromCurves(curves, (double)_divide);

            if (points.Count == 0)
            {
                TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok);
                return false;
            }

            // loop through each point and use reference intersector to get topo Z
            foreach (XYZ pt in points)
            {
                // The 3 inputs for ReferenceIntersector are:
                // A filter specifying that only ceilings will be reported
                // A FindReferenceTarget option indicating what types of objects to report
                // A 3d view (see http://wp.me/p2X0gy-2p for more info on how this works)

                var startPt = pt;
                var endPtUp = new XYZ(pt.X, pt.Y, pt.Z + 100d);
                var dirUp = (endPtUp - startPt).Normalize();
                var referenceIntersector = new ReferenceIntersector(_topoSolid.Id, FindReferenceTarget.All, _v3d);
                var obstructionsOnUnboundLineUp = referenceIntersector.Find(startPt, dirUp);
                XYZ point = null;
                var gRefWithContext = obstructionsOnUnboundLineUp.FirstOrDefault();
                var gRef = gRefWithContext.GetReference();
                point = gRef.GlobalPoint;
                points1.Add(point);
            }

            if (CleanupTopoPoints == true)
            {
                PointsUtils.DeleteTopoPointsWithinCurves(curves, _doc, _topoSolid);
            }

            // we now have points with correct Z values.
            // delete duplicate points and add to topo
            // TODO: Check duplicates in more robust way.
            // testing 0.1 instead of 0.01
            var comparer = new XyzEqualityComparer(); // (0.1)
            points1 = points1.Distinct(comparer).ToList();
            using (var t = new Transaction(_doc, "add points"))
            {
                t.Start();

                foreach (XYZ p in points1)
                {
                    _topoSolid.GetSlabShapeEditor().DrawPoint(p);
                }

                t.Commit();
            }


#else
            using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
            {
                tes.Start(_topoSurface.Id);
                var opt = new Options();
                opt.ComputeReferences = true;
                points = PointsUtils.GetPointsFromCurves(curves, (double)_divide);
                if (points.Count == 0)
                {
                    TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok);
                    tes.Cancel();
                    return false;
                }

                // loop through each point and use reference intersector to get topo Z
                foreach (XYZ pt in points)
                {
                    // The 3 inputs for ReferenceIntersector are:
                    // A filter specifying that only ceilings will be reported
                    // A FindReferenceTarget option indicating what types of objects to report
                    // A 3d view (see http://wp.me/p2X0gy-2p for more info on how this works)

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
                    PointsUtils.DeleteTopoPointsWithinCurves(curves, _doc, _topoSurface);
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
#endif

        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }
}
