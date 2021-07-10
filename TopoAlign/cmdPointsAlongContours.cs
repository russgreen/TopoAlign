using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class cmdPointsAlongContours : IExternalCommand
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
        private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
        private View3D _v3d;
        private Units _docUnits;

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
                    //_offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#else
                    _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                    _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
                    _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#endif

                    //first save the settings for next time
                    cSettings.DivideEdgeDistance = _divide;
                    cSettings.SaveSettings();

                    if (PointsAlongContours() == false)
                    {
                        return Result.Failed;
                    }
                }
            }

            // Return Success
            return Result.Succeeded;
        }

        private bool PointsAlongContours()
        {
            //check if the active view is a 3D view
            if (_doc.ActiveView is View3D)
            {
                _v3d = (View3D)_doc.ActiveView;
            }
            else
            {
                TaskDialog.Show("Points along contours", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
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
                _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception ex)
            {
                return false;
            }

            List<Curve> curves;
            curves = new List<Curve>();
            try
            {
                foreach (Reference r in _uidoc.Selection.PickObjects(ObjectType.Element, lineFilter, "Select countour lines(s) to add topo points along"))
                {
                    var curve = default(Curve);
                    ModelLine modelLine = _doc.GetElement(r) as ModelLine;
                    ModelCurve modelCurve = _doc.GetElement(r) as ModelCurve;
                    try
                    {
                        curve = modelLine.GeometryCurve;
                    }
                    catch (Exception ex)
                    {
                    }

                    try
                    {
                        curve = modelCurve.GeometryCurve;
                    }
                    catch (Exception ex)
                    {
                    }

                    curves.Add(curve);
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            // sort the curves and make contiguous
            // Dim creapp As Autodesk.Revit.Creation.Application = doc.Application.Create
            // Try
            // CurveUtils.SortCurvesContiguous(m_Curves)
            // Catch ex As Exception
            // TaskDialog.Show("Points along contours", "The lines selected must all be connected", TaskDialogCommonButtons.Ok)
            // Return Result.Failed
            // End Try

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    var opt = new Options();
                    opt.ComputeReferences = true;
                    points = GetPointsFromCurves(curves);
                    if (points.Count == 0)
                    {
                        TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok);
                        tes.Cancel();
                        return false;
                    }

                    // we now have points with correct Z values.
                    // delete duplicate points and add to topo
                    // TODO: Check duplicates in more robust way.
                    // texting 0.1 instead of 0.01
                    var comparer = new XyzEqualityComparer(); // (0.1)
                    using (var t = new Transaction(_doc, "add points"))
                    {
                        t.Start();
                        _topoSurface.AddPoints(points.Distinct(comparer).ToList());
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

        private IList<XYZ> GetPointsFromCurves(List<Curve> curves)
        {
            var points = new List<XYZ>();

            foreach (Curve m_Curve in curves)
            {
                int i = m_Curve.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in m_Curve.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z + (double)_offset);
                        points.Add(pt1);
                    }
                }
                else
                {
                    double len = m_Curve.ApproximateLength;
                    if (len > (double)_divide)
                    {
                        var pt0 = new XYZ(m_Curve.Tessellate()[0].X, m_Curve.Tessellate()[0].Y, m_Curve.Tessellate()[0].Z);
                        var pt1 = new XYZ(m_Curve.Tessellate()[1].X, m_Curve.Tessellate()[1].Y, m_Curve.Tessellate()[1].Z);
                        foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, (double)_divide))
                        {
                            var p = new XYZ(pt.X, pt.Y, pt.Z + (double)_offset);
                            points.Add(p);
                        }
                    }
                    else
                    {
                        foreach (XYZ pt in m_Curve.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z + (double)_offset);
                            points.Add(pt1);
                        }
                    }
                }
            }

            return points;
        }
    }
}
