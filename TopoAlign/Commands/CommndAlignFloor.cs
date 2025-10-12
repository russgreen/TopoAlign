using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using TopoAlign.Comparers;
using TopoAlign.Geometry;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
public class CommndAlignFloor : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommndAlignFloor>();

    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app; 
    private Document _doc;
    private Selection _sel;
    private decimal _offset;
    private Floor _floor;


#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif

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
        log.Information("{command}", nameof(CommndAlignFloor));

        cSettings = new Models.Settings();
        cSettings.LoadSettings();

        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _app = _uiapp.Application;
        _doc = _uidoc.Document;
        _sel = _uidoc.Selection;

#if REVIT2018 || REVIT2019 || REVIT2020
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif
        using (FormDivideLines frm = new())
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
            _floor = _doc.GetElement(refElement) as Floor;
        }
        catch (Exception)
        {
            return false;
        }

        //get the topo
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

        ResetFloorSlape();

        var options = new Options();
        var geometryElement = _floor.get_Geometry(options);
        IList<CurveLoop> floorBoundaryCurves = GetGeometryOutline(geometryElement);

#if REVIT2024_OR_GREATER
        //create a sub-region that matches the floor to get all the topo surface points
        var siteSubDivision = CreateSubDivision(floorBoundaryCurves);

        if(siteSubDivision is null)
        {
            return false;
        }

        var points = GetPointsFromSubDivision(siteSubDivision, offset);
#else
        //create a sub-region that matches the floor to get all the topo surface points
        SiteSubRegion siteSubRegion = CreateSubRegion(floorBoundaryCurves);

        if (siteSubRegion == null)
        {
            return false;
        }

        //add the points to the floor     
        var comparer = new XyzEqualityComparer(); // (0.01)

        var points = (IEnumerable<XYZ>)siteSubRegion.TopographySurface.GetPoints().Distinct(comparer);
#endif

        using (var t = new Transaction(_doc, "Add points to floor"))
        {
            t.Start();
            FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
            t.SetFailureHandlingOptions(failureHandlingOptions);

#if REVIT2025_OR_GREATER
            var editor = _floor.GetSlabShapeEditor();
            editor.Enable();

            var levelOffset = _floor.GetParameter(ParameterUtils.GetParameterTypeId(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)).AsDouble();


            foreach (var p in points)
            {
                try 
                {
                    editor.AddPoint(new XYZ(p.X, p.Y, p.Z - levelOffset)); 
                } 
                catch 
                { 
                    // ignore interior failures  
                }
            }

            _doc.Regenerate(); // force update

            var verts = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            var cornerVerts = verts.Where(v => v.VertexType == SlabShapeVertexType.Corner).ToList();

            var xyComparer = new XyEqualityComparer();
            foreach (var cv in cornerVerts)
            {
                var match = points.FirstOrDefault(p => xyComparer.Equals(p, cv.Position));
                if (match != null)
                {
                    try
                    {
                        var offsetZ = match.Z - levelOffset;
                        editor.ModifySubElement(cv, offsetZ);
                    }
                    catch
                    { }
                }
            }

#elif REVIT2024
            foreach (XYZ pt in points)
            {
                //don't add the offset.  We already added it to the sub-division
                var pt1 = new XYZ(pt.X, pt.Y, pt.Z);
                _floor.GetSlabShapeEditor().DrawPoint(pt1); 
             }
#else
            foreach (XYZ pt in points)
            {
                var pt1 = new XYZ(pt.X, pt.Y, pt.Z + offset);
                _floor.SlabShapeEditor.DrawPoint(pt1);                
            }
#endif

#if REVIT2024_OR_GREATER
            _doc.Delete(siteSubDivision.Id);
#else
            _doc.Delete(siteSubRegion.TopographySurface.Id);
#endif

            t.Commit();
        }

        return true;
    }

#if !REVIT2024_OR_GREATER
    private SiteSubRegion CreateSubRegion(IList<CurveLoop> floorBoundaryCurves)
    {
        try
        {
            SiteSubRegion siteSubRegion;

            using (var t = new Transaction(_doc, "Make SubRegion"))
            {
                t.Start();
                FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
                failureHandlingOptions.SetFailuresPreprocessor((IFailuresPreprocessor)new FailureHandler());
                t.SetFailureHandlingOptions(failureHandlingOptions);
                siteSubRegion = SiteSubRegion.Create(_doc, floorBoundaryCurves, _topoSurface.Id);
                t.Commit();
            }

            return siteSubRegion;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("existing SiteSubRegions"))
            {
                var td = new TaskDialog("Error aligning element")
                {
                    MainContent = "You cannot align an element that crosses a subregion",
                    CommonButtons = TaskDialogCommonButtons.Cancel,
                };
                td.Show();

            }

            return null;
        }
    }
#endif

#if REVIT2024_OR_GREATER
    private Toposolid CreateSubDivision(IList<CurveLoop> floorBoundaryCurves)
    {
        Toposolid siteSubDivision = null;
        using (var t = new Transaction(_doc, "Make SubRegion"))
        {
            t.Start();
            var failureHandlingOptions = t.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
            t.SetFailureHandlingOptions(failureHandlingOptions);

            try
            {
                siteSubDivision = _topoSolid.CreateSubDivision(_doc, floorBoundaryCurves);

#if REVIT2024
                siteSubDivision.get_Parameter(BuiltInParameter.TOPOSOLID_SUBDIVIDE_HEIGNT)
                    .Set(0.001);
#elif REVIT2025
                siteSubDivision.get_Parameter(BuiltInParameter.TOPOSOLID_SUBDIVIDE_HEIGHT)
                    .Set(0.0001);
#endif
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);

                t.RollBack();
            }

            t.Commit();
        }

        return siteSubDivision;
    }
#endif

    private void ResetFloorSlape()
    {
        //reset the floor 
        using (var t = new Transaction(_doc, "Reset floor"))
        {
            t.Start();
            var failureHandlingOptions = t.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
            t.SetFailureHandlingOptions(failureHandlingOptions);

#if REVIT2024_OR_GREATER
            _floor.GetSlabShapeEditor().ResetSlabShape();
#else
            _floor.SlabShapeEditor.ResetSlabShape();
#endif
            t.Commit();
        }
    }

#if REVIT2024_OR_GREATER
    private List<XYZ> GetPointsFromSubDivision(Toposolid siteSubDivision, double offset)
    {
        var raw = new List<XYZ>();

        //get the siteSubDivision GeometryElement
        var geometryElement = siteSubDivision.get_Geometry(new Options());
        var faces = GetTopFaces(geometryElement);

        foreach (var face in faces)
        {
            // 1) Collect triangulation vertices (interior-heavy)
            var meshVerts = face.Triangulate().Vertices;
            foreach (var v in meshVerts)
            {
                raw.Add(v);
            }

            // 2) Explicitly sample boundary curves to ensure edge/corner points exist
            //    Using endpoints and a midpoint is enough to ensure we hit corners and edges.
            var loops = face.GetEdgesAsCurveLoops();
            foreach (var loop in loops)
            {
                foreach (var curve in loop)
                {
                    // endpoints
                    raw.Add(curve.Evaluate(0.0, true));
                    raw.Add(curve.Evaluate(1.0, true));

                    // midpoint
                    raw.Add(curve.Evaluate(0.5, true));
                }
            }
        }

        // De-dup by XY to stabilize the set and avoid minor z/precision duplicates and add offset to Z
        var points = new List<XYZ>();
        foreach(var point in raw.Distinct(new XyEqualityComparer()).ToList())
        {
            points.Add(new XYZ(point.X, point.Y, point.Z + offset));
        }

        return points;
    }
     
    private IList<Face> GetTopFaces(GeometryElement geometryElement)
    {
        var faces = new List<Face>();

        foreach (GeometryObject geometryObject in geometryElement)
        {
            if (geometryObject is Solid)
            {
                foreach (Face face in (geometryObject as Solid).Faces)
                {
                    if (GeometryCalculation.IsTopFace(face))
                    {
                        faces.Add(face);
                    }
                }
            }
        }

        return faces;
    }
#endif

    private IList<CurveLoop> GetGeometryOutline(GeometryElement geometryElement)
    {
        //get the outline of the floor to make a sub-region on the topo
        //curveLoops = (IList<CurveLoop>)new List<CurveLoop>();
        //curveLoopList = (IList<CurveLoop>)new List<CurveLoop>();
        var face1 = (Face)null;
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

        return face1.GetEdgesAsCurveLoops().ToList();
    }

}
