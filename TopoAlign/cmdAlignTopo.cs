using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
//using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TopoAlign
{
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
        private Element _element;
        private Edge _edge;
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

#if REVIT2018
            var revitVersion = "2018";
#elif REVIT2019
            var revitVersion = "2019";
#elif REVIT2020
            var revitVersion = "2020";
#elif REVIT2021
            var revitVersion = "2021";
#elif REVIT2022
            var revitVersion = "2022";
#endif
            //Analytics.TrackEvent($"Revit Version {revitVersion}");

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

            using (FormAlignTopo frm = new FormAlignTopo())
            {
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
                    _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#else
                    _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                    _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
                    _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#endif

                    //first save the settings for next time
                    cSettings.SingleElement = frm.rdoElem.Checked;
                    cSettings.DivideEdgeDistance = _divide;
                    cSettings.VerticalOffset = _offset;
                    cSettings.CleanTopoPoints = frm.chkRemoveInside.Checked;
                    cSettings.TopFace = frm.rdoTop.Checked;
                    cSettings.SaveSettings();

                    if(AlignTopo(frm.rdoTop.Checked, frm.rdoEdge.Checked) == false)
                    {
                        return Result.Failed;
                    }
                }
            }

            // Return Success
            return Result.Succeeded;
        }
   
        private bool AlignTopo(bool TopFace = true, bool UseEdge = false)
        {
            var fh = new FailureHandler();
            var topoFilter = new TopoPickFilter();
            var elemFilter = new ElemPickFilter();

            IList<XYZ> points = new List<XYZ>();
            IList<XYZ> points1 = new List<XYZ>();
            IList<XYZ> xYZs1 = new List<XYZ>();

            //first get the topo surface
            try
            {
                var refToposurface = _uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                _topoSurface = _doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
            }
            catch (Exception)
            {
                return false;
            }

            List<Edge> edges = new List<Edge>();
            List<Curve> curves = new List<Curve>();
            if(UseEdge == false)
            {
                //we're using elements rather than edges
                try
                {
                    var refElement = _uidoc.Selection.PickObject(ObjectType.Element, elemFilter, "Select an object to align to");
                    _element = _doc.GetElement(refElement);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                //we're using edges
                try
                {
                    edges = new List<Edge>();
                    curves = new List<Curve>();
                    foreach (Reference r in _uidoc.Selection.PickObjects(ObjectType.Edge, "Select edge(s) to align to"))
                    {
                        var element = _doc.GetElement(r.ElementId);
                        Edge selectedEdge = element.GetGeometryObjectFromReference(r) as Edge;
                        var m_Curve = selectedEdge.AsCurve();
                        if (element is FamilyInstance fi)
                        {
                            var the_list_of_the_joined = JoinGeometryUtils.GetJoinedElements(_doc, fi);
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
                    return false;
                }
            }

            try
            {
                using (var tes = new Autodesk.Revit.DB.Architecture.TopographyEditScope(_doc, "Align topo"))
                {
                    tes.Start(_topoSurface.Id);
                    var opt = new Options
                    {
                        ComputeReferences = true
                    };
                    if (UseEdge == false)
                    {
                        if (_element is FamilyInstance fi)
                        {
                            points = GetPointsFromFamily(fi.get_Geometry(opt), TopFace);
                        }
                        else
                        {
                            if (cSettings.CleanTopoPoints == true)
                                CleanupTopoPoints(_element);

                            points = GetPointsFromElement(_element, TopFace);
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
                        points = PointsUtils.GetPointsFromCurves(curves, (double)_divide, -(double)_offset);
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
                return false;
            }

            return true;
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
                                if (Util.IsTopFace(f) == true)
                                {
                                    faces.Add(f);
                                }
                            }
                            else if (Util.IsBottomFace(f) == true)
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

                        if (cSettings.CleanTopoPoints == true)
                            CleanupTopoPoints(solid);
                    }
                }
            }

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

            return points;
        }

        private void CleanupTopoPoints(Element element)
        {
            // try and get boundary and cleanup topo
            switch (element.Category.Name ?? "")
            {
                case "Floors":
                    break;
                case "Roofs":
                    break;
                case "Walls":
                    break;
                case "Pads":
                    break;
                default:
                {
                    // don't cleanup
                    return;
                }
            }

            var polygons = new List<List<XYZ>>();
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
            var bb = element.get_BoundingBox(v);
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

        private void CleanupTopoPoints(Solid solid)
        {
            throw new NotImplementedException();
        }

        #region Slab Boundary Debug
        private const double _boundaryOffset = 0.1d;

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
                if (f is PlanarFace pf)
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
                            v -= _boundaryOffset * XYZ.BasisZ;
                            vertices.Add(v);
                        }
                    }

                    q -= _boundaryOffset * XYZ.BasisZ;
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

                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid solid)
                    {
                        GetBoundary(polygons, solid);
                    }
                }
            }

            return polygons;
        }


        #endregion
    }
}
