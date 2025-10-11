using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using TopoAlign.Comparers;
using TopoAlign.Geometry;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandPointsAtIntersection : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandPointsAtIntersection>();

    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Document _doc;
    private Selection _sel;
    //private decimal _offset;
    private decimal _divide;
    //private Element _element;
    //private Edge _edge;
    private Face _face;
    private View3D _v3d;
    private Units _docUnits;

#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif


#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits; 
    //private DisplayUnitType _useDisplayUnits;
#else
    private ForgeTypeId _docDisplayUnits;
    //private ForgeTypeId _useDisplayUnits;
#endif

    public Models.Settings cSettings;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandPointsAtIntersection));

        cSettings = new Models.Settings();
        cSettings.LoadSettings();
        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _app = _uiapp.Application;
        _doc = _uidoc.Document;
        _sel = _uidoc.Selection;

        //check entitlement
        //if (CheckEntitlement.LicenseCheck(_app) == false)
        //{
        //    return Result.Cancelled;
        //}

#if REVIT2018 || REVIT2019 || REVIT2020
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        _docUnits = _doc.GetUnits();
        _docDisplayUnits = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif

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
            Autodesk.Revit.UI.TaskDialog.Show("Points at intersection", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
            return false;
        }

        var fh = new FailureHandler();

        try
        {
            var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, new TopoPickFilter(), "Select a topographic surface");

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

        GeometryObject geoObjFace;

        try
        {
            var refFace = _uidoc.Selection.PickObject(ObjectType.Face, new FacePickFilter(_doc), "Select a face");
            geoObjFace = _doc.GetElement(refFace).GetGeometryObjectFromReference(refFace);
            _face = geoObjFace as Face;
        }
        catch (Exception)
        {
            return false;
        }

        List<g3.Triangle3d> topoTriangles = new();

#if REVIT2024_OR_GREATER
        if (_topoSolid != null)
        {
            topoTriangles = TriTriIntersect.TrianglesFromTopo(_topoSolid);
        }
#else
        if(_topoSurface != null)
        {
            topoTriangles = TriTriIntersect.TrianglesFromTopo(_topoSurface);
        }
#endif

        List<g3.Triangle3d> faceTriangles = TriTriIntersect.TrianglesFromGeoObj(_face);

        var intersections = TriTriIntersect.IntersectTriangleLists(topoTriangles, faceTriangles, (double)_divide);

        if (intersections is null || intersections.Count == 0)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Topo Align", "Unable to get a suitable list of intersections from the faces selected.", TaskDialogCommonButtons.Ok);
            return false;
        }

        List<XYZ> points = PointsUtils.GetPointsFromVector3ds(intersections);

        if (points.Count == 0)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the faces selected.", TaskDialogCommonButtons.Ok);
            return false;
        }

        // delete duplicate points
        var comparer = new XyzEqualityComparer(); // (0.01)
        var uniquePoints = points.Distinct(comparer).ToList();

        try
        {
#if REVIT2024_OR_GREATER
            if (_topoSolid != null)
            {
                using (var t = new Transaction(_doc, "add points"))
                {
                    t.Start();

                    _topoSolid.GetSlabShapeEditor().AddPoints(uniquePoints);

                    t.Commit();
                }
            }
#else
        if(_topoSurface != null)
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
              
                    using (var t = new Transaction(_doc, "add points"))
                    {
                        t.Start();
                        if(_topoSurface != null)
                        {
                            _topoSurface.AddPoints(uniquePoints);
                        }


                        t.Commit();
                    }

                    tes.Commit(fh);
                }
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
