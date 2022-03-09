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

namespace TopoAlign;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class cmdPointsAtIntersection : IExternalCommand
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
    private Face _face;
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

        /**
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

                if (PointsOnSurfaceAtIntersection() == false)
                {
                    return Result.Failed;
                }
            }
        }
        **/

        if (PointsOnSurfaceAtIntersection() == false)
        {
            return Result.Failed;
        }

        // Return Success
        return Result.Succeeded;
    }


    private bool PointsOnSurfaceAtIntersection()
    {
        //check the active view is a 3D view
        if (_doc.ActiveView is View3D)
        {
            _v3d = (View3D)_doc.ActiveView;
        }
        else
        {
            TaskDialog.Show("Points at intersection", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
            return false;
        }

        var fh = new FailureHandler();

        try
        {
            var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, new TopoPickFilter(), "Select a topographic surface");
            _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
        }
        catch (Exception ex)
        {
            return false;
        }
                    
        GeometryObject geoObjFace;

        try
        {
            var refFace = _uidoc.Selection.PickObject(ObjectType.Face, new FacePickFilter(_doc), "Select a face");
            geoObjFace = _doc.GetElement(refFace).GetGeometryObjectFromReference(refFace);
            _face = geoObjFace as Face;
        }
        catch (Exception ex)
        {
            return false;
        }

        List<g3.Triangle3d> topoTriangles = TriTriIntersect.TrianglesFromTopo(_topoSurface);
        List<g3.Triangle3d> faceTriangles = TriTriIntersect.TrianglesFromGeoObj(_face);

        var intersections = TriTriIntersect.IntersectTriangleLists(topoTriangles, faceTriangles, (double)_divide);

        try
        {
            using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
            {
                tes.Start(_topoSurface.Id);

                List<XYZ> points = PointsUtils.GetPointsFromVector3ds(intersections);

                if (points.Count == 0)
                {
                    TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the faces selected.", TaskDialogCommonButtons.Ok);
                    tes.Cancel();
                    return false;
                }

                // delete duplicate points
                var comparer = new XyzEqualityComparer(); // (0.01)
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
}
