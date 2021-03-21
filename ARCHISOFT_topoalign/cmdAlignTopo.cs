/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ARCHISOFT_topoalign
{
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */

    [Transaction(TransactionMode.Manual)]
    public class cmdAlignTopo : IExternalCommand
    {
        private UIApplication _uiapp;
        private UIDocument _uidoc;
        private Autodesk.Revit.ApplicationServices.Application _app;
        private Document _doc;
        private Selection _sel;
        private decimal _offset;
        private decimal _divide;
        private Util _util = new Util();
        private Element _element;
        private Edge _edge;
        private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
        private Units _docUnits;
        /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
        private ForgeTypeId _docDisplayUnits;
        private ForgeTypeId _useDisplayUnits;
        /* TODO ERROR: Skipped EndIfDirectiveTrivia */
        public Settings cSettings;

        /// <summary>
    /// The one and only method required by the IExternalCommand interface, the main entry point for every external command.
    /// </summary>
    /// <param name="commandData">Input argument providing access to the Revit application, its documents and their properties.</param>
    /// <param name="message">Return argument to display a message to the user in case of error if Result is not Succeeded.</param>
    /// <param name="elements">Return argument to highlight elements on the graphics screen if Result is not Succeeded.</param>
    /// <returns>Cancelled, Failed or Succeeded Result code.</returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string revitVersion;
            /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia */
            revitVersion = "2021";
            /* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */
            // Analytics.TrackEvent($"Revit Version {revitVersion}")
            // Crashes.GenerateTestCrash()

            cSettings = new Settings();
            cSettings.LoadSettings();
            _uiapp = commandData.Application;
            _uidoc = _uiapp.ActiveUIDocument;
            _app = _uiapp.Application;
            _doc = _uidoc.Document;
            _sel = _uidoc.Selection;

            /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
            _docUnits = _doc.GetUnits();
            _docDisplayUnits = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
            /* TODO ERROR: Skipped EndIfDirectiveTrivia */

            var frm = new frmAlignTopo();
            frm.rdoElem.Checked = cSettings.SingleElement;
            frm.rdoEdge.Checked = !frm.rdoElem.Checked;
            _divide = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)cSettings.DivideEdgeDistance, _docDisplayUnits));
            _offset = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)cSettings.VerticalOffset, _docDisplayUnits));
            if (_divide > frm.nudDivide.Maximum)
            {
                frm.nudDivide.Value = frm.nudDivide.Maximum;
            }
            else
            {
                frm.nudDivide.Value = _divide;
            }

            if (_offset > frm.nudVertOffset.Maximum)
            {
                frm.nudVertOffset.Value = frm.nudVertOffset.Maximum;
            }
            else
            {
                frm.nudVertOffset.Value = _offset;
            }

            frm.chkRemoveInside.Checked = cSettings.CleanTopoPoints;
            frm.rdoTop.Checked = cSettings.TopFace;
            frm.rdoBottom.Checked = !frm.rdoTop.Checked;

            /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */            // pupulate the display units dropdown
            foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
            {
                frm.DisplayUnitTypecomboBox.Items.AddRange(new object[] { displayUnitType });
                Debug.WriteLine(LabelUtils.GetLabelForUnit(displayUnitType));
                frm.DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelForUnit(displayUnitType));
            }

            frm.DisplayUnitTypecomboBox.SelectedItem = _docDisplayUnits;
            frm.DisplayUnitcomboBox.SelectedIndex = frm.DisplayUnitTypecomboBox.SelectedIndex;
            /* TODO ERROR: Skipped EndIfDirectiveTrivia */
            if (frm.ShowDialog() == DialogResult.OK)
            {
                /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
                _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
                _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
                /* TODO ERROR: Skipped EndIfDirectiveTrivia */
                // first save the settings for next time
                cSettings.SingleElement = frm.rdoElem.Checked;
                cSettings.DivideEdgeDistance = _divide;
                cSettings.VerticalOffset = _offset;
                cSettings.CleanTopoPoints = frm.chkRemoveInside.Checked;
                cSettings.TopFace = frm.rdoTop.Checked;
                cSettings.SaveSettings();
                if (AlignTopo(frm.rdoTop.Checked, frm.rdoEdge.Checked) == false)
                {
                    return Result.Failed;
                    return default;
                }
            }

            return Result.Succeeded;
        }

        private bool AlignTopo(bool TopFace = true, bool UseEdge = false)
        {
            bool retval = true;
            var fh = new FailureHandler();
            var topoFilter = new TopoPickFilter();
            var elemFilter = new ElemPickFilter();
            IList<XYZ> points = new List<XYZ>();
            IList<XYZ> points1 = new List<XYZ>();
            IList<XYZ> xYZs1 = new List<XYZ>();
            try
            {
                var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception ex)
            {
                return false;
            }


            // Dim m_Elements As List(Of Element)
            List<Edge> m_Edges;
            var m_Curves = default(List<Curve>);
            if (UseEdge == false)
            {
                try
                {
                    var refElement = _uidoc.Selection.PickObject(ObjectType.Element, elemFilter, "Select an object to align to");
                    m_Element = _doc.GetElement(refElement);
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else
            {
                try
                {
                    // we're just picking edges
                    m_Edges = new List<Edge>();
                    m_Curves = new List<Curve>();
                    foreach (Reference r in _uidoc.Selection.PickObjects(ObjectType.Edge, "Select edge(s) to align to"))
                    {
                        var m_Element = _doc.GetElement(r.ElementId);
                        Edge m_SelectedEdge = m_Element.GetGeometryObjectFromReference(r) as Edge;
                        var m_Curve = m_SelectedEdge.AsCurve();
                        FamilyInstance fi = m_Element as FamilyInstance;
                        if (fi is object)
                        {
                            var the_list_of_the_joined = JoinGeometryUtils.GetJoinedElements(_doc, fi);
                            if (the_list_of_the_joined.Count == 0)
                            {
                                m_Curve = m_Curve.CreateTransformed(fi.GetTransform());
                            }
                        }

                        m_Curves.Add(m_Curve);
                    }
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    FamilyInstance fi = m_Element as FamilyInstance;
                    var opt = new Options();
                    opt.ComputeReferences = true;
                    if (UseEdge == false)
                    {
                        if (fi is object)
                        {
                            points = GetPointsFromFamily(fi.get_Geometry(opt), TopFace);
                        }
                        else
                        {
                            if (cSettings.CleanTopoPoints == true)
                                CleanupTopoPoints(m_Element);
                            points = GetPointsFromElement(m_Element, TopFace);
                        }

                        if (points.Count == 0)
                        {
                            TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the object. Try picking edges", TaskDialogCommonButtons.Ok);
                            tes.Cancel();
                            return false;
                        }
                    }
                    else
                    {
                        points = GetPointsFromCurves(m_Curves);
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
                TaskDialog.Show("Align topo", ex.Message);
                // Crashes.TrackError(ex)
                return false;
            }

            return retval;
        }

        private List<XYZ> GetPointsFromElement(Element e, bool TopFace)
        {
            var points = new List<XYZ>();
            var opt = new Options();
            opt.ComputeReferences = true;
            var m_GeometryElement = e.get_Geometry(opt);
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
                        if (TopFace == true)
                        {
                            if (Util.IsTopFace(f) == true)
                            {
                                m_Faces.Add(f);
                            }
                        }
                        else if (Util.IsBottomFace(f) == true)
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
                                    var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                    points.Add(pt1);
                                }
                            }
                            else
                            {
                                double len = m_edge.ApproximateLength;
                                if (len > (double)_divide)
                                {
                                    var pt0 = new XYZ(m_edge.Tessellate()[0].X, m_edge.Tessellate()[0].Y, m_edge.Tessellate()[0].Z);
                                    var pt1 = new XYZ(m_edge.Tessellate()[1].X, m_edge.Tessellate()[1].Y, m_edge.Tessellate()[1].Z);
                                    foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, (double)_divide))
                                    {
                                        var p = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                        points.Add(p);
                                    }
                                }
                                else
                                {
                                    foreach (XYZ pt in m_edge.Tessellate())
                                    {
                                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
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

        private List<XYZ> GetPointsFromFamily(GeometryElement e, bool TopFace)
        {
            var points = new List<XYZ>();

            // we have selected a family instance so try and get the geometry from it
            PlanarFace m_Face = null;
            var m_Faces = new List<Face>();

            // 'force the bottom edge to be selected
            // TopFace = False

            foreach (GeometryObject m_GeometryObject in e)
            {
                GeometryInstance m_GeometryInstance = m_GeometryObject as GeometryInstance;
                var m_GeometryInstanceElement = m_GeometryInstance.GetInstanceGeometry();
                foreach (GeometryObject m_GeometryInstanceObject in m_GeometryInstanceElement)
                {
                    Solid m_Solid = m_GeometryInstanceObject as Solid;
                    if (m_Solid == null)
                    {
                        return points;
                    }
                    else
                    {
                        foreach (Face f in m_Solid.Faces)
                        {
                            if (TopFace == true)
                            {
                                if (Util.IsTopFace(f) == true)
                                {
                                    m_Faces.Add(f);
                                }
                            }
                            else if (Util.IsBottomFace(f) == true)
                            {
                                m_Faces.Add(f);
                            }
                        }

                        foreach (Face f in m_Faces)
                        {
                            PlanarFace pf = f as PlanarFace;
                            if (pf is object)
                            {
                                if (m_Face == null)
                                    m_Face = pf;
                                if (pf.Origin.Z < m_Face.Origin.Z)
                                {
                                    m_Face = pf;
                                }
                            }
                        }

                        if (cSettings.CleanTopoPoints == true)
                            CleanupTopoPoints(m_Solid);
                    }
                }
            }

            // For Each lf As Face In m_LowestFaces
            foreach (EdgeArray ea in m_Face.EdgeLoops)
            {
                // For Each ea As EdgeArray In lf.EdgeLoops
                foreach (Edge m_edge in ea)
                {
                    int i = m_edge.Tessellate().Count;
                    if (i > 2)
                    {
                        foreach (XYZ pt in m_edge.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                            points.Add(pt1);
                        }
                    }
                    else
                    {
                        double len = m_edge.ApproximateLength;
                        if (len > (double)_divide)
                        {
                            var pt0 = new XYZ(m_edge.Tessellate()[0].X, m_edge.Tessellate()[0].Y, m_edge.Tessellate()[0].Z);
                            var pt1 = new XYZ(m_edge.Tessellate()[1].X, m_edge.Tessellate()[1].Y, m_edge.Tessellate()[1].Z);
                            foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, (double)_divide))
                            {
                                var p = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                points.Add(p);
                            }
                        }
                        else
                        {
                            foreach (XYZ pt in m_edge.Tessellate())
                            {
                                var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                points.Add(pt1);
                            }
                        }
                    }
                } // m_edge
            } // ea
              // Next 'lf

            return points;
        }

        private IList<XYZ> GetPointsFromEdge(Edge m_Edge)
        {
            var points = new List<XYZ>();
            int i = m_Edge.Tessellate().Count;
            if (i > 2)
            {
                foreach (XYZ pt in m_Edge.Tessellate())
                {
                    var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                    points.Add(pt1);
                }
            }
            else
            {
                double len = m_Edge.ApproximateLength;
                if (len > (double)_divide)
                {
                    var pt0 = new XYZ(m_Edge.Tessellate()[0].X, m_Edge.Tessellate()[0].Y, m_Edge.Tessellate()[0].Z);
                    var pt1 = new XYZ(m_Edge.Tessellate()[1].X, m_Edge.Tessellate()[1].Y, m_Edge.Tessellate()[1].Z);
                    foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, (double)_divide))
                    {
                        var p = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                        points.Add(p);
                    }
                }
                else
                {
                    foreach (XYZ pt in m_Edge.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                        points.Add(pt1);
                    }
                }
            }

            return points;
        }

        private IList<XYZ> GetPointsFromEdges(IList<Edge> m_Edges)
        {
            var points = new List<XYZ>();
            foreach (Edge m_Edge in m_Edges)
            {
                int i = m_Edge.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in m_Edge.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                        points.Add(pt1);
                    }
                }
                else
                {
                    double len = m_Edge.ApproximateLength;
                    if (len > (double)_divide)
                    {
                        var pt0 = new XYZ(m_Edge.Tessellate()[0].X, m_Edge.Tessellate()[0].Y, m_Edge.Tessellate()[0].Z);
                        var pt1 = new XYZ(m_Edge.Tessellate()[1].X, m_Edge.Tessellate()[1].Y, m_Edge.Tessellate()[1].Z);
                        foreach (XYZ pt in Util.DividePoints(pt0, pt1, len, (double)_divide))
                        {
                            var p = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                            points.Add(p);
                        }
                    }
                    else
                    {
                        foreach (XYZ pt in m_Edge.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                            points.Add(pt1);
                        }
                    }
                }
            }

            return points;
        }

        private IList<XYZ> GetPointsFromCurves(List<Curve> m_Curves)
        {
            var points = new List<XYZ>();
            foreach (Curve m_Curve in m_Curves)
            {
                int i = m_Curve.Tessellate().Count;
                if (i > 2)
                {
                    foreach (XYZ pt in m_Curve.Tessellate())
                    {
                        var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
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
                            var p = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                            points.Add(p);
                        }
                    }
                    else
                    {
                        foreach (XYZ pt in m_Curve.Tessellate())
                        {
                            var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                            points.Add(pt1);
                        }
                    }
                }
            }

            return points;
        }

        private void CleanupTopoPoints(Solid s)
        {
        }

        private void CleanupTopoPoints(Element elem)
        {
            // try and get boundary and cleanup topo
            switch (elem.Category.Name ?? "")
            {
                case var @case when @case == "Floors":
                {
                    break;
                }

                case var case1 when case1 == "Roofs":
                {
                    break;
                }

                case var case2 when case2 == "Walls":
                {
                    break;
                }

                case var case3 when case3 == "Pads":
                {
                    break;
                }

                default:
                {
                    // don't cleanup
                    return;
                }
            }

            var polygons = new List<List<XYZ>>();
            var opt = new Options();
            opt.ComputeReferences = true;
            var m_GeometryElement = elem.get_Geometry(opt);
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
                        if (Util.IsBottomFace(f) == true)
                        {
                            m_Faces.Add(f);
                        }
                    }
                }

                foreach (Face f in m_Faces)
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
                                    var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                    polygon.Add(pt1);
                                }
                            }
                            else
                            {
                                foreach (XYZ pt in m_edge.Tessellate())
                                {
                                    var pt1 = new XYZ(pt.X, pt.Y, pt.Z - (double)_offset);
                                    polygon.Add(pt1);
                                }
                            }
                        }

                        polygons.Add(polygon);
                    }
                }
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

            // Get topopoints withing bounding box of element
            Autodesk.Revit.DB.View v = null;
            var bb = elem.get_BoundingBox(v);
            var min = new XYZ(bb.Min.X - 1d, bb.Min.Y - 1d, _topoSurface.get_BoundingBox(v).Min.Z);
            var max = new XYZ(bb.Max.X + 1d, bb.Max.Y + 1d, _topoSurface.get_BoundingBox(v).Max.Z);
            var outline = new Outline(min, max);
            var points = new List<XYZ>();
            // intPoints = TryCast(m_TopoSurface.FindPoints(outline), List(Of XYZ))
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


        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        private const double _offset = 0.1d;

        /// <summary>
    /// Determine the boundary polygons of the lowest
    /// horizontal planar face of the given solid.
    /// </summary>
    /// <param name="polygons">Return polygonal boundary
    /// loops of lowest horizontal face, i.e. profile of
    /// circumference and holes</param>
    /// <param name="solid">Input solid</param>
    /// <returns>False if no horizontal planar face was
    /// found, else true</returns>
        private static bool GetBoundary(List<List<XYZ>> polygons, Solid solid)
        {
            PlanarFace lowest = null;
            var faces = solid.Faces;
            foreach (Face f in faces)
            {
                PlanarFace pf = f as PlanarFace;
                // If pf IsNot Nothing AndAlso Util.IsHorizontal(pf) Then
                if (pf is object)
                {
                    if (lowest is null || pf.Origin.Z < lowest.Origin.Z)
                    {
                        lowest = pf;
                    }
                }
            }

            if (lowest is object)
            {
                XYZ p;
                var q = XYZ.Zero;
                bool first;
                int i;
                int n;
                var loops = lowest.EdgeLoops;
                foreach (EdgeArray loop in loops)
                {
                    var vertices = new List<XYZ>();
                    first = true;
                    foreach (Edge e in loop)
                    {
                        var points = e.Tessellate();
                        p = points[0];
                        if (!first)
                        {
                            Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" + " to equal previous end point");
                        }

                        n = points.Count;
                        q = points[n - 1];
                        var loopTo = n - 2;
                        for (i = 0; i <= loopTo; i++)
                        {
                            var v = points[i];
                            v -= _offset * XYZ.BasisZ;
                            vertices.Add(v);
                        }
                    }

                    q -= _offset * XYZ.BasisZ;
                    Debug.Assert(q.IsAlmostEqualTo(vertices[0]), "expected last end point to equal" + " first start point");
                    polygons.Add(vertices);
                }
            }

            return lowest is object;
        }

        /// <summary>
    /// Return all floor slab boundary loop polygons
    /// for the given floors, offset downwards from the
    /// bottom floor faces by a certain amount.
    /// </summary>
        public static List<List<XYZ>> GetFloorBoundaryPolygons(List<Element> floors, Options opt)
        {
            var polygons = new List<List<XYZ>>();
            foreach (Floor floor in floors)
            {
                var geo = floor.get_Geometry(opt);

                // GeometryObjectArray objects = geo.Objects; // 2012
                // foreach( GeometryObject obj in objects ) // 2012

                foreach (GeometryObject obj in geo)
                {
                    // 2013
                    Solid solid = obj as Solid;
                    if (solid is object)
                    {
                        GetBoundary(polygons, solid);
                    }
                }
            }

            return polygons;
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */

    }
}