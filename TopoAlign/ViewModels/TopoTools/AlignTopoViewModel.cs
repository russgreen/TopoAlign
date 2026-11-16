using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Reflection;
using TopoAlign;
using TopoAlign.Comparers;
using TopoAlign.Extensions;
using TopoAlign.Geometry;
using TopoAlign.Services;
using TopoAlign.Views;

namespace TopoAlign.ViewModels.TopoTools;
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
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;


#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits; 
#else
    private ForgeTypeId _docDisplayUnits = null!;
#endif

    Services.Settings _settings = new();

    private Element? _element;
    //private Edge _edge;

#if REVIT2024_OR_GREATER
    private Toposolid? _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface? _topoSurface;
#endif

    //private Units _docUnits;


    public AlignTopoViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

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
        Divide = Decimal.Round(Convert.ToDecimal(((double)_settings.DivideEdgeDistance).ToUnit(_docDisplayUnits)), 2);
        Offset = Decimal.Round(Convert.ToDecimal(((double)_settings.VerticalOffset).ToUnit(_docDisplayUnits)), 2);
    }


    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        Divide = Helpers.UnitConversion.Convert(Divide, oldValue.DisplayUnit, newValue.DisplayUnit);
        Offset = Helpers.UnitConversion.Convert(Offset, oldValue.DisplayUnit, newValue.DisplayUnit);
    }

    [RelayCommand]
    private void AlignTopo()
    {
        IsWindowVisible = System.Windows.Visibility.Hidden;

        //converts the units to internal units
        Divide = Convert.ToDecimal(((double)Divide).FromUnit(SelectedDisplayUnit.DisplayUnit));
        Offset = Convert.ToDecimal(((double)Offset).FromUnit(SelectedDisplayUnit.DisplayUnit)); ;

        try
        {
            AlignTopoSurface();
        }
        catch (OperationCanceledException)
        {
            // The user cancelled from the progress window; any open transaction has been rolled back.
        }

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

        SelectedDisplayUnit = DisplayUnits.Where(x => x.DisplayUnit == _docDisplayUnits).FirstOrDefault() ?? DisplayUnits.First();
    }

    private void AlignTopoSurface()
    {
        var fh = new FailureHandler();
        var topoFilter = new TopoPickFilter();
        var elemFilter = new ElemPickFilter();

        GetTopoSurface(topoFilter);

        List<Element> elements = new();
        List<Edge> edges = new();
        List<Curve> curves = new();
        GetElementsOrEdges(elemFilter, ref elements, ref edges, ref curves);

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
                foreach (Element element in elements)
                {
                    if (element is FamilyInstance fi)
                    {
                        if (CleanTopoPoints == true)
                        {
                            CleanupTopoPoints(fi);
                        }

                        foreach (var point in GetPointsFromFamily(fi.get_Geometry(opt), TopFace))
                        {
                            points.Add(point);
                        }
                        
                    }
                    else
                    {
                        if (CleanTopoPoints == true)
                        {
                            CleanupTopoPoints(element);
                        }

                        foreach (var point in GetPointsFromElement(element, TopFace))
                        {
                            points.Add(point);
                        }
                        
                    }
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
                    var selectedEdge = element.GetGeometryObjectFromReference(r) as Edge
                                       ?? throw new InvalidOperationException("The reference is not an edge.");
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

    private void GetElementsOrEdges(ElemPickFilter elemFilter, ref List<Element> elements, ref List<Edge> edges, ref List<Curve> curves)
    {
        if (SingleElement)
        {
            //we're using elements rather than edges
            try
            {
                foreach(Reference r in App.CachedUiApp.ActiveUIDocument.Selection.PickObjects(ObjectType.Element, elemFilter, "Select objects to align to"))
                {
                    var element = App.RevitDocument.GetElement(r);
                    elements.Add(element);
                }
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
                    var selectedEdge = element.GetGeometryObjectFromReference(r) as Edge
                                       ?? throw new InvalidOperationException("The reference is not an edge.");
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
            Solid? m_Solid = m_GeometryObject as Solid;
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
                        PointsUtils.AddEdgePoints(m_edge, (double)Offset, (double)Divide, points);
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
        PlanarFace? face = null;
        var faces = new List<Face>();

        foreach (GeometryObject geometryObject in geometryElements)
        {
            var geometryInstance = geometryObject as GeometryInstance;
            if (geometryInstance is null)
            {
                continue;
            }

            var geometryInstanceElement = geometryInstance.GetInstanceGeometry();
            foreach (GeometryObject geometryInstanceObject in geometryInstanceElement)
            {
                Solid? solid = geometryInstanceObject as Solid;
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
                    PointsUtils.AddEdgePoints(m_edge, (double)Offset, (double)Divide, points);
                }
            }
        }


        return points;
    }

    private void CleanupTopoPoints(Element element)
    {
        var opt = new Options
        {
            ComputeReferences = true
        };
        var geometryElement = element.get_Geometry(opt);

        // One entry per bottom face; each holds that face's loops (outer + holes), oriented and closed.
        var faceLoops = FootprintBuilder.GetFaceLoops(geometryElement);
        if (faceLoops.Count == 0)
        {
            return;
        }

        // Plan extents of the footprint, padded slightly, used as a cheap prefilter.
        double minU = double.MaxValue, minV = double.MaxValue, maxU = double.MinValue, maxV = double.MinValue;
        foreach (var loops in faceLoops)
        {
            foreach (var loop in loops)
            {
                for (int i = 0; i < loop.Size; i++)
                {
                    var uv = loop.Item(i);
                    minU = Math.Min(minU, uv.U);
                    minV = Math.Min(minV, uv.V);
                    maxU = Math.Max(maxU, uv.U);
                    maxV = Math.Max(maxV, uv.V);
                }
            }
        }

        const double pad = 0.01d;
        minU -= pad;
        minV -= pad;
        maxU += pad;
        maxV += pad;

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
            topoPoints = new List<XYZ>(_topoSurface.GetInteriorPoints());
        }
#endif


        foreach (XYZ pt in topoPoints)
        {
            if (pt.X >= minU && pt.X <= maxU && pt.Y >= minV && pt.Y <= maxV)
            {
                topoPointsInBoundingBox.Add(pt);
            }
        }


        // Check each point against all loops: boundary-inclusive even-odd rule
        var pointsInPolygon = new List<XYZ>();
        using (var pf = ProgressService.Start("Analyzing topo points.", topoPointsInBoundingBox.Count, "{0} points of " + topoPointsInBoundingBox.Count + " processed..."))
        {
            foreach (XYZ pt in topoPointsInBoundingBox)
            {
                pf.ThrowIfCancelled();

                var pUV = PointsUtils.Flatten(pt);

                // Inside if within ANY bottom face (boundary inclusive, even-odd within a face for holes)
                bool inside = false;
                foreach (var loops in faceLoops)
                {
                    if (PointInPoly.LoopsContain(loops, pUV))
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside)
                {
                    pointsInPolygon.Add(pt);
                }

                pf.Increment();
            }
        }

        //using (var t = new Transaction(App.RevitDocument, "debugging points"))
        //{
        //    t.Start();

        //    foreach(var pt in pointsInPolygon)
        //    {
        //        pt.Visualize(App.RevitDocument);
        //    }

        //    t.Commit();
        //}

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

    // New: build per-loop UV polygons (preserve loops)
    private static List<UVArray> GetUvLoops(List<List<XYZ>> polygons)
    {
        var loops = new List<UVArray>(polygons.Count);
        foreach (var poly in polygons)
        {
            var uv = new List<UV>(poly.Count);
            foreach (var p in poly)
            {
                uv.Add(PointsUtils.Flatten(p));
            }

            loops.Add(new UVArray(uv));
        }
        return loops;
    }

    private List<List<XYZ>> GetPolygonsFromGeometryElement(GeometryElement geometryElement)
    {
        var polygons = new List<List<XYZ>>();

        foreach (GeometryObject geometryObject in geometryElement)
        {
            Solid? solid = geometryObject as Solid;
            var faces = new List<Face>();
            if (solid == null)
            {
                //check if we can get SymbolGeometry from GeometryObject
                var geometryInstance = geometryObject as GeometryInstance;
                if (geometryInstance is null)
                {
                    continue;
                }

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
