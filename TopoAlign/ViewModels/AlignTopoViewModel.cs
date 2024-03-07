using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Reflection;
using TopoAlign.Comparers;
using TopoAlign.Geometry;

namespace TopoAlign.ViewModels;
internal partial class AlignTopoViewModel : BaseViewModel
{
    public string WindowTitle { get; private set; }

    public bool Edge => !SingleElement;
    public bool BottomFace => !TopFace;

    [ObservableProperty]
    private System.Windows.Visibility _isWindowVisible = System.Windows.Visibility.Visible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Edge))]
    private bool _singleElement = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BottomFace))]
    private bool _topFace = true;

    [ObservableProperty]
    private decimal _divide = 5000;

    [ObservableProperty]
    private decimal _offset = 50;

    [ObservableProperty]
    private bool _cleanTopoPoints = true;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit;


#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits; 
#else
    private ForgeTypeId _docDisplayUnits;
#endif

    Models.Settings _settings = new();

    private Element _element;
    //private Edge _edge;

#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif

    //private Units _docUnits;


    public AlignTopoViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

        WindowTitle = $"TopoAlign {informationVersion}";

#if REVIT2018 || REVIT2019 || REVIT2020
        _docDisplayUnits = App.RevitDocument.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        _docDisplayUnits = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif

        LoadSettings();

        PopulateDisplayUnitList();
    }

    private void LoadSettings()
    {
        _settings.LoadSettings();
        SingleElement = _settings.SingleElement;
        CleanTopoPoints = _settings.CleanTopoPoints;
        TopFace = _settings.TopFace;
        Divide = Decimal.Round(Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)_settings.DivideEdgeDistance, _docDisplayUnits)), 2);
        Offset = Decimal.Round(Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)_settings.VerticalOffset, _docDisplayUnits)), 2);
    }


    [RelayCommand]
    private void AlignTopo()
    {
        IsWindowVisible = System.Windows.Visibility.Hidden;

        //converts the units to internal units
        Divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)Divide, SelectedDisplayUnit.DisplayUnit));
        Offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)Offset, SelectedDisplayUnit.DisplayUnit)); ;

        AlignTopoSurface();

        SaveSettings();

        this.OnClosingRequest();
        return;
    }

    private void SaveSettings()
    {
        _settings.SingleElement = SingleElement;
        _settings.CleanTopoPoints = CleanTopoPoints;
        _settings.TopFace = TopFace;
        _settings.DivideEdgeDistance = Divide;
        _settings.VerticalOffset = Offset;
        _settings.SaveSettings();
    }

    private void PopulateDisplayUnitList()
    {
#if REVIT2018 || REVIT2019 || REVIT2020
        foreach (DisplayUnitType displayUnitType in UnitUtils.GetValidDisplayUnits(UnitType.UT_Length))
        {
            var displayUnit = new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelFor(displayUnitType)
            };

            DisplayUnits.Add(displayUnit);
        }
#else
        foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            var displayUnit = new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelForUnit(displayUnitType)
            };

            DisplayUnits.Add(displayUnit);
        }
#endif

        SelectedDisplayUnit = DisplayUnits.Where(x => x.DisplayUnit == _docDisplayUnits).FirstOrDefault();
    }

    private void AlignTopoSurface()
    {
        var fh = new FailureHandler();
        var topoFilter = new TopoPickFilter();
        var elemFilter = new ElemPickFilter();

        GetTopoSurface(topoFilter);

        List<Edge> edges = new();
        List<Curve> curves = new();
        GetElementOrEdges(elemFilter, ref edges, ref curves);

        IList<XYZ> points = new List<XYZ>();
        IList<XYZ> points1 = new List<XYZ>();
        IList<XYZ> xYZs1 = new List<XYZ>();

        try
        {
            var opt = new Options
            {
                ComputeReferences = true
            };

            if (SingleElement)
            {
                if (_element is FamilyInstance fi)
                {
                    if (CleanTopoPoints == true)
                    {
                        CleanupTopoPoints(fi);
                    }

                    points = GetPointsFromFamily(fi.get_Geometry(opt), TopFace);
                }
                else
                {
                    if (CleanTopoPoints == true)
                    {
                        CleanupTopoPoints(_element);
                    }

                    points = GetPointsFromElement(_element, TopFace);
                }

                if (points.Count == 0)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the object. Try picking edges", TaskDialogCommonButtons.Ok);
                    return;
                }
            }
            else
            {
                points = PointsUtils.GetPointsFromCurves(curves, (double)Divide, -(double)Offset);
            }

            // delete duplicate points
            var comparer = new XyzEqualityComparer(); // (0.01)
            var uniquePoints = points.Distinct(comparer).ToList();

#if REVIT2024_OR_GREATER
            if (_topoSolid != null)
            {
                using (var t = new Transaction(App.RevitDocument, "add points"))
                {
                    t.Start();

                    FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
                    failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
                    t.SetFailureHandlingOptions(failureHandlingOptions);

                    _topoSolid.GetSlabShapeEditor().AddPoints(uniquePoints);

                    t.Commit();
                }
            }
#else
            if (_topoSurface != null)
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(App.RevitDocument, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    
                    using (var t = new Transaction(App.RevitDocument, "add points"))
                    {
                        t.Start();
                        _topoSurface.AddPoints(uniquePoints);
                        t.Commit();
                    }

                    tes.Commit(fh);
                }
            }
#endif

        }
        catch (Exception ex)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Align topo", ex.Message);
            return;
        }
    }

    private void GetTopoSurface(TopoPickFilter topoFilter)
    {
        try
        {
            var refToposurface = App.CachedUiApp.ActiveUIDocument.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");

#if REVIT2024_OR_GREATER
            _topoSolid = App.RevitDocument.GetElement(refToposurface) as Toposolid;
#else
            _topoSurface = App.RevitDocument.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
#endif
        }
        catch (Exception)
        {
            return;
        }
    }

    private void GetElementOrEdges(ElemPickFilter elemFilter, ref List<Edge> edges, ref List<Curve> curves)
    {
        if (SingleElement)
        {
            //we're using elements rather than edges
            try
            {
                var refElement = App.CachedUiApp.ActiveUIDocument.Selection.PickObject(ObjectType.Element, elemFilter, "Select an object to align to");
                _element = App.RevitDocument.GetElement(refElement);
            }
            catch (Exception)
            {
                return;
            }
        }
        else
        {
            //we're using edges
            try
            {
                edges = new List<Edge>();
                curves = new List<Curve>();
                foreach (Reference r in App.CachedUiApp.ActiveUIDocument.Selection.PickObjects(ObjectType.Edge, "Select edge(s) to align to"))
                {
                    var element = App.RevitDocument.GetElement(r.ElementId);
                    Edge selectedEdge = element.GetGeometryObjectFromReference(r) as Edge;
                    var m_Curve = selectedEdge.AsCurve();
                    if (element is FamilyInstance fi)
                    {
                        var the_list_of_the_joined = JoinGeometryUtils.GetJoinedElements(App.RevitDocument, fi);
                        if (the_list_of_the_joined.Count == 0)
                        {
                            m_Curve = m_Curve.CreateTransformed(fi.GetTransform());
                        }
                    }

                    curves.Add(m_Curve);
                }
            }
            catch (Exception)
            {
                return;
            }
        }
    }


    private IList<XYZ> GetPointsFromElement(Element element, bool topFace)
    {
        var points = new List<XYZ>();
        var opt = new Options
        {
            ComputeReferences = true
        };
        var m_GeometryElement = element.get_Geometry(opt);
        foreach (GeometryObject m_GeometryObject in m_GeometryElement)
        {
            Solid m_Solid = m_GeometryObject as Solid;
            var m_Faces = new List<Face>();
            if (m_Solid == null)
            {
            }
            else
            {
                foreach (Face f in m_Solid.Faces)
                {
                    if (topFace == true)
                    {
                        if (GeometryCalculation.IsTopFace(f) == true)
                        {
                            m_Faces.Add(f);
                        }
                    }
                    else if (GeometryCalculation.IsBottomFace(f) == true)
                    {
                        m_Faces.Add(f);
                    }
                }
            }

            foreach (Face f in m_Faces)
            {
                foreach (EdgeArray ea in f.EdgeLoops)
                {
                    foreach (Edge m_edge in ea)
                    {
                        int i = m_edge.Tessellate().Count;
                        if (i > 2)
                        {
                            foreach (XYZ pt in m_edge.Tessellate())
                            {
                                var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                points.Add(pt1);
                            }
                        }
                        else
                        {
                            double len = m_edge.ApproximateLength;
                            if (len > (double)Divide)
                            {
                                var pt0 = new XYZ(m_edge.Tessellate()[0].X, m_edge.Tessellate()[0].Y, m_edge.Tessellate()[0].Z);
                                var pt1 = new XYZ(m_edge.Tessellate()[1].X, m_edge.Tessellate()[1].Y, m_edge.Tessellate()[1].Z);
                                foreach (XYZ pt in GeometryCalculation.DividePoints(pt0, pt1, len, (double)Divide))
                                {
                                    var p = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                    points.Add(p);
                                }
                            }
                            else
                            {
                                foreach (XYZ pt in m_edge.Tessellate())
                                {
                                    var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                    points.Add(pt1);
                                }
                            }
                        }
                    }
                }
            }
        }

        return points;
    }

    private IList<XYZ> GetPointsFromFamily(GeometryElement geometryElements, bool topFace)
    {
        var points = new List<XYZ>();

        // we have selected a family instance so try and get the geometry from it
        PlanarFace face = null;
        var faces = new List<Face>();

        foreach (GeometryObject geometryObject in geometryElements)
        {
            GeometryInstance geometryInstance = geometryObject as GeometryInstance;
            var geometryInstanceElement = geometryInstance.GetInstanceGeometry();
            foreach (GeometryObject geometryInstanceObject in geometryInstanceElement)
            {
                Solid solid = geometryInstanceObject as Solid;
                if (solid == null)
                {
                    return points;
                }
                else
                {
                    foreach (Face f in solid.Faces)
                    {
                        if (topFace == true)
                        {
                            if (GeometryCalculation.IsTopFace(f) == true)
                            {
                                faces.Add(f);
                            }
                        }
                        else if (GeometryCalculation.IsBottomFace(f) == true)
                        {
                            faces.Add(f);
                        }
                    }

                    foreach (Face f in faces)
                    {
                        if (f is PlanarFace pf)
                        {
                            if (face == null)
                                face = pf;
                            if (pf.Origin.Z < face.Origin.Z)
                            {
                                face = pf;
                            }
                        }
                    }

                    //if (cSettings.CleanTopoPoints == true)
                    //    CleanupTopoPoints(solid);
                }
            }
        }

        if (face != null)
        {
            // For Each lf As Face In m_LowestFaces
            foreach (EdgeArray ea in face.EdgeLoops)
            {
                // For Each ea As EdgeArray In lf.EdgeLoops
                foreach (Edge m_edge in ea)
                {
                    int i = m_edge.Tessellate().Count;
                    if (i > 2)
                    {
                        foreach (XYZ pt in m_edge.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                            points.Add(pt1);
                        }
                    }
                    else
                    {
                        double len = m_edge.ApproximateLength;
                        if (len > (double)Divide)
                        {
                            var pt0 = new XYZ(m_edge.Tessellate()[0].X, m_edge.Tessellate()[0].Y, m_edge.Tessellate()[0].Z);
                            var pt1 = new XYZ(m_edge.Tessellate()[1].X, m_edge.Tessellate()[1].Y, m_edge.Tessellate()[1].Z);
                            foreach (XYZ pt in GeometryCalculation.DividePoints(pt0, pt1, len, (double)Divide))
                            {
                                var p = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                points.Add(p);
                            }
                        }
                        else
                        {
                            foreach (XYZ pt in m_edge.Tessellate())
                            {
                                var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                points.Add(pt1);
                            }
                        }
                    }
                }
            }
        }


        return points;
    }

    private void CleanupTopoPoints(Element element)
    {
        ////try and get boundary and cleanup topo
        //switch (element.Category.Name ?? "")
        //{
        //    case "Floors":
        //        break;
        //    case "Roofs":
        //        break;
        //    case "Walls":
        //        break;
        //    case "Pads":
        //        break;
        //    default:
        //        {
        //            // don't cleanup
        //            return;
        //        }
        //}
                   

        var opt = new Options
        {
            ComputeReferences = true
        };
        var geometryElement = element.get_Geometry(opt);

        var polygons = new List<List<XYZ>>();
        polygons = GetPolygonsFromGeometryElement(geometryElement);

        UVArray polygon = GetPolygon(polygons);

        // Get topo points withing bounding box of element
        Autodesk.Revit.DB.View v = null;
        var bb = element.get_BoundingBox(v);

        XYZ min = new();
        XYZ max = new();

#if REVIT2024_OR_GREATER
        if (_topoSolid != null)
        {
            min = new XYZ(bb.Min.X - 1d, bb.Min.Y - 1d, _topoSolid.get_BoundingBox(v).Min.Z);
            max = new XYZ(bb.Max.X + 1d, bb.Max.Y + 1d, _topoSolid.get_BoundingBox(v).Max.Z);
        }
#else
        if(_topoSurface != null)
        {
            min = new XYZ(bb.Min.X - 1d, bb.Min.Y - 1d, _topoSurface.get_BoundingBox(v).Min.Z);
            max = new XYZ(bb.Max.X + 1d, bb.Max.Y + 1d, _topoSurface.get_BoundingBox(v).Max.Z);
        }
#endif

        var outline = new Outline(min, max);
        var topoPoints = new List<XYZ>();
        var topoPointsInBoundingBox = new List<XYZ>();

#if REVIT2024_OR_GREATER
        if (_topoSolid != null)
        {
            var vts = _topoSolid.GetSlabShapeEditor().SlabShapeVertices;
            foreach (SlabShapeVertex shv in vts)
            {
                var p = new XYZ(shv.Position.X, shv.Position.Y, shv.Position.Z);
                topoPoints.Add(p);
            }
        }
#else
        if (_topoSurface != null)
        {
            topoPoints = _topoSurface.GetInteriorPoints() as List<XYZ>;
        }
#endif

        foreach (XYZ pt in topoPoints)
        {
            if (outline.Contains(pt, 0.000000001d))
            {
                topoPointsInBoundingBox.Add(pt);
            }
        }

        // Check each point to see if point is with 2D boundary
        var pointsInPolygon = new List<XYZ>();
        using (var pf = new ProgressForm("Analyzing topo points.", "{0} points of " + topoPointsInBoundingBox.Count + " processed...", topoPointsInBoundingBox.Count))
        {
            foreach (XYZ pt in topoPointsInBoundingBox)
            {
                if (PointInPoly.PointInPolygon(polygon, PointsUtils.Flatten(pt)) == true)
                {
                    pointsInPolygon.Add(pt);
                }

                pf.Increment();
            }
        }

        // Remove topo points if any are found within the polygon
        if (pointsInPolygon.Count > 0)
        {
#if REVIT2024_OR_GREATER

            if (_topoSolid != null)
            {
                using (var t = new Transaction(App.RevitDocument, "removing points"))
                {
                    t.Start();

                    foreach (XYZ p in pointsInPolygon)
                    {
                        topoPoints.Remove(p);
                    }

                    var editor = _topoSolid.GetSlabShapeEditor();

                    editor.ResetSlabShape();
                    editor.Enable();

                    App.RevitDocument.Regenerate();

                    editor.AddPoints(topoPoints);

                    t.Commit();
                }
            }
#else
            var fh = new FailureHandler();            
            if(_topoSurface != null)
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(App.RevitDocument, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    using (var t = new Transaction(App.RevitDocument, "removing points"))
                    {
                        t.Start();

                        if (_topoSurface != null)
                        {
                            _topoSurface.DeletePoints(pointsInPolygon);
                        }

                        t.Commit();
                    }
                    tes.Commit(fh);
                }
            }
#endif

        }
    }

    private static UVArray GetPolygon(List<List<XYZ>> polygons)
    {
        var flat_polygons = PointsUtils.Flatten(polygons);
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

        return uvArr;
    }

    private List<List<XYZ>> GetPolygonsFromGeometryElement(GeometryElement geometryElement)
    {
        var polygons = new List<List<XYZ>>();

        foreach (GeometryObject geometryObject in geometryElement)
        {
            Solid solid = geometryObject as Solid;
            var faces = new List<Face>();
            if (solid == null)
            {
                //check if we can get SymbolGeometry from GeometryObject
                var geometryInstance = geometryObject as GeometryInstance;
                var geometryInstanceElement = geometryInstance.GetInstanceGeometry();

                foreach (GeometryObject geometryInstanceObject in geometryInstanceElement)
                {
                    solid = geometryInstanceObject as Solid; 
                    if (solid != null)
                    {
                        foreach (Face f in solid.Faces)
                        {
                            if (GeometryCalculation.IsBottomFace(f) == true)
                            {
                                faces.Add(f);
                            }
                        }
                    }

                }
            }
            else
            {
                foreach (Face f in solid.Faces)
                {
                    if (GeometryCalculation.IsBottomFace(f) == true)
                    {
                        faces.Add(f);
                    }
                }
            }

            foreach (Face f in faces)
            {
                var polygon = new List<XYZ>();
                foreach (EdgeArray ea in f.EdgeLoops)
                {
                    foreach (Edge m_edge in ea)
                    {
                        int i = m_edge.Tessellate().Count;
                        if (i > 2)
                        {
                            foreach (XYZ pt in m_edge.Tessellate())
                            {
                                var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                polygon.Add(pt1);
                            }
                        }
                        else
                        {
                            foreach (XYZ pt in m_edge.Tessellate())
                            {
                                var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)Offset);
                                polygon.Add(pt1);
                            }
                        }
                    }

                    polygons.Add(polygon);
                }
            }
        }

        return polygons;
    }

}
