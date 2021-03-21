/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace ARCHISOFT_topoalign
{
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
    [Transaction(TransactionMode.Manual)]
    public class cmdPointsAtIntersection : IExternalCommand
    {
        private UIApplication uiapp;
        private UIDocument uidoc;
        private Autodesk.Revit.ApplicationServices.Application app;
        private Document doc;
        private Selection sel;
        private Util clsUtil = new Util();
        private Autodesk.Revit.DB.Architecture.TopographySurface m_TopoSurface;
        private View3D v3d;
        public Settings cSettings;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            cSettings = new Settings();
            cSettings.LoadSettings();
            uiapp = commandData.Application;
            uidoc = uiapp.ActiveUIDocument;
            app = uiapp.Application;
            doc = uidoc.Document;
            sel = uidoc.Selection;
            if (doc.ActiveView is View3D)
            {
                v3d = (View3D)doc.ActiveView;
            }
            else
            {
                TaskDialog.Show("Points on surface", "You must be in a 3D view", TaskDialogCommonButtons.Ok);
                return Result.Failed;
            }

            var fh = new FailureHandler();
            var topoFilter = new TopoPickFilter();
            Mesh meshTopo;
            // Dim meshElem As Mesh

            // Plane – PlanarFace
            // Cylinder – CylindricalFace
            // Cone – ConicalFace
            // Revolved Face – RevolvedFace
            // Ruled Surface – RuledFace
            // Hermite Face – HermiteFace
            PlanarFace pf;
            CylindricalFace cf;
            RevolvedFace rvf;
            RuledFace rf;
            HermiteFace hf;
            try
            {
                var refToposurface = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface");
                m_TopoSurface = doc.GetElement(refToposurface) as Autodesk.Revit.DB.Architecture.TopographySurface;
                meshTopo = m_TopoSurface.get_Geometry(new Options()).First(q => q is Mesh) as Mesh;
            }
            catch (Exception ex)
            {
                return Result.Failed;
            }

            try
            {
                var refElem = uidoc.Selection.PickObject(ObjectType.Face, "Select face that intersects");
                var geoObj = doc.GetElement(refElem).GetGeometryObjectFromReference(refElem);

                // Do we have a planarface, cylindricalface, hermiteface or ruledsurface
                pf = geoObj as PlanarFace;
                cf = geoObj as CylindricalFace;
                hf = geoObj as HermiteFace;
                rvf = geoObj as RevolvedFace;
                rf = geoObj as RuledFace;
            }

            // If pf IsNot Nothing Then meshElem = pf.Triangulate
            // If cf IsNot Nothing Then meshElem = cf.Triangulate
            // If hf IsNot Nothing Then meshElem = hf.Triangulate
            // If rvf IsNot Nothing Then meshElem = rvf.Triangulate
            // If rf IsNot Nothing Then meshElem = rf.Triangulate

            catch (Exception ex)
            {
                return Result.Failed;
            }

            List<Curve> m_Curves;
            m_Curves = new List<Curve>();
            IList<XYZ> m_Points = new List<XYZ>();
            var args = new List<XYZ>(3);
            try
            {
                for (int i = 0, loopTo = meshTopo.NumTriangles - 1; i <= loopTo; i++)
                {
                    var triangle = meshTopo.get_Triangle(i);

                    // Dim p1 As XYZ = triangle.Vertex(0)
                    // Dim p2 As XYZ = triangle.Vertex(1)
                    // Dim p3 As XYZ = triangle.Vertex(2)
                    // args.Clear()
                    // args.Add(p1)
                    // args.Add(p2)
                    // args.Add(p3)

                    using (var t = new Transaction(doc, "Create Temp Form"))
                    {
                        t.Start();

                        // Dim pl As Plane = Autodesk.Revit.DB.Plane.CreateByThreePoints(triangle.Vertex(0), triangle.Vertex(1), triangle.Vertex(2))

                        // TODO: Find a way to make a face from the mesh triangle
                        var m_face = default(PlanarFace);

                        // Dim geoOpt As New Autodesk.Revit.DB.Options
                        // Dim geoElem As Autodesk.Revit.DB.GeometryElement = m_form.Geometry(geoOpt)

                        // For Each geoObj As GeometryObject In geoElem
                        // m_face = TryCast(geoObj, PlanarFace)
                        // Next

                        var m_curve = default(Curve);
                        if (pf is object)
                            pf.Intersect(m_face, out m_curve);
                        if (cf is object)
                            cf.Intersect(m_face, out m_curve);
                        if (hf is object)
                            hf.Intersect(m_face, out m_curve);
                        if (rvf is object)
                            rvf.Intersect(m_face, out m_curve);
                        if (rf is object)
                            rf.Intersect(m_face, out m_curve);
                        if (m_curve is object)
                        {
                            m_Curves.Add(m_curve);
                        }

                        t.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                return Result.Failed;
            }

            return default;
        }
    }
}