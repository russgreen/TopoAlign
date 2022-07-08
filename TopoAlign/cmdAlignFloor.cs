using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace TopoAlign;

[Transaction(TransactionMode.Manual)]
public class cmdAlignFloor : IExternalCommand
{
    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Autodesk.Revit.DB.Document _doc;
    private Selection _sel;
    private decimal _offset;
    private Floor _floor;
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
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

            frm.nudVertOffset.Value = 0;
            frm.nudDivide.Enabled = false;

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
                _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#else
                _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#endif

                //first save the settings for next time
                cSettings.VerticalOffset = _offset;
                cSettings.SaveSettings();

                if (AlignFloor((double)_offset) == false)
                {
                    return Result.Failed;
                }
            }
        }

        // Return Success
        return Result.Succeeded;
    }

    private bool AlignFloor(double offset = 0)
    {
        var fh = new FailureHandler();
        var topoFilter = new TopoPickFilter();
        var elemFilter = new FloorPickFilter();

        //get the floor to align
        try
        {
            var refElement = _uidoc.Selection.PickObject(ObjectType.Element, elemFilter, "Select an object to align to");
            _floor = _doc.GetElement(refElement) as Autodesk.Revit.DB.Floor;
        }
        catch (Exception)
        {
            return false;
        }

        //get the topo surface
        try
        {
            var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
            _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
        }
        catch (Exception)
        {
            return false;
        }

        //reset the floor 
        using (var t = new Transaction(_doc, "Reset floor"))
        {
            t.Start();

            _floor.SlabShapeEditor.ResetSlabShape();

           t.Commit();
        }


        //get the outline of the floor to a sub-region on the topo
        Options options = new Options();
        GeometryElement geometryElement = _floor.get_Geometry(options);
        IList<CurveLoop> curveLoops = (IList<CurveLoop>)new List<CurveLoop>();
        IList<CurveLoop> curveLoopList = (IList<CurveLoop>)new List<CurveLoop>();
        Face face1 = (Face)null;


        foreach (GeometryObject geometryObject in geometryElement)
        {
            if (geometryObject is Solid)
            {
                foreach (Face face2 in (geometryObject as Solid).Faces)
                {
                    if (face2.Evaluate(face2.GetBoundingBox().Max).Z == face2.Evaluate(face2.GetBoundingBox().Min).Z)
                        face1 = face2;
                }
            }
        }

        curveLoops = (IList<CurveLoop>)face1.GetEdgesAsCurveLoops().ToList<CurveLoop>();
        curveLoopList = face1.GetEdgesAsCurveLoops();

        //create a sub-region that matches the floor to get all the topo surface points
        SiteSubRegion siteSubRegion = (SiteSubRegion)null;
        using (var t = new Transaction(_doc, "Make SubRegion"))
        {
            t.Start();
            FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor((IFailuresPreprocessor)new FailureHandler());
            t.SetFailureHandlingOptions(failureHandlingOptions);
            siteSubRegion = SiteSubRegion.Create(_doc, curveLoops, _topoSurface.Id);
            t.Commit();
        }       

        //add the points to the floor     
        var comparer = new XyzEqualityComparer(); // (0.01)
        
        var points = (IEnumerable<XYZ>)siteSubRegion.TopographySurface.GetPoints().Distinct(comparer);
        using (var t = new Transaction(_doc, "Add points to floor"))
        {
            t.Start();
            FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor((IFailuresPreprocessor)new FailureHandler());
            t.SetFailureHandlingOptions(failureHandlingOptions);

            foreach (XYZ pt in points)
            {
                var pt1 = new XYZ(pt.X, pt.Y, pt.Z + offset);
                _floor.SlabShapeEditor.DrawPoint(pt1);
            }

            //delete the sub-region.  we don't need it anymore
            _doc.Delete(siteSubRegion.TopographySurface.Id);
            
            t.Commit();
        }

        return true;
    }
}
