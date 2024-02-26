using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Diagnostics;
using TopoAlign.Comparers;
using TopoAlign.Extensions;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandResetTopoRegion : IExternalCommand
{
    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Document _doc;
    private string _sourcePhaseName = "Existing";
    private string _targetPhaseName = "New Construction";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        // Crashes.GenerateTestCrash()

        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _app = _uiapp.Application;
        _doc = _uidoc.Document;

        //check entitlement
        //if (CheckEntitlement.LicenseCheck(_app) == false)
        //{
        //    return Result.Cancelled;
        //}

        // check if the active view is a plan view
        if (_doc.ActiveView.ViewType != ViewType.FloorPlan)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Topo Reset", "Please make sure you are in a plan view before running this command", TaskDialogCommonButtons.Ok);
            return Result.Failed;
        }

        var frm = new FormProjectPhases();

#if REVIT2024_OR_GREATER
        // load phases to combo
        var comboPhases = new Dictionary<long, string>();

        foreach (Phase phase in _doc.Phases)
            comboPhases.Add(phase.Id.Value, phase.Name);
#else
        // load phases to combo
        var comboPhases = new Dictionary<int, string>();

        foreach (Phase phase in _doc.Phases)
            comboPhases.Add(phase.Id.IntegerValue, phase.Name);
#endif

        if (comboPhases.Count > 0)
        {
            frm.cboPhaseSource.DisplayMember = "Value";
            frm.cboPhaseSource.ValueMember = "Value"; // "Key"
            frm.cboPhaseSource.DataSource = new BindingSource(comboPhases, null);
            frm.cboPhaseSource.SelectedIndex = 0;
            frm.cboPhaseTarget.DisplayMember = "Value";
            frm.cboPhaseTarget.ValueMember = "Value"; // "Key"
            frm.cboPhaseTarget.DataSource = new BindingSource(comboPhases, null);
            frm.cboPhaseTarget.SelectedIndex = 1;
            frm.btnOK.Enabled = true;
        }

        if (frm.ShowDialog() == DialogResult.OK)
        {
            _sourcePhaseName = frm.cboPhaseSource.SelectedValue.ToString();
            _targetPhaseName = frm.cboPhaseTarget.SelectedValue.ToString();

            var sourcePhase = new FilteredElementCollector(_doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(q => (q.Name ?? "") == (_sourcePhaseName ?? ""));

            var targetPhase = new FilteredElementCollector(_doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(q => (q.Name ?? "") == (_targetPhaseName ?? ""));

#if REVIT2024_OR_GREATER
            IList<Toposolid> topoList = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Toposolid)
                .OfType<Toposolid>()
                .ToList();
#else
            IList<Element> topoList = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Topography)
                .ToList();
#endif

            var fh = new FailureHandler();
            var frm1 = new FormResetTopo();

            try
            {

                // load existing topo(s) to combo
#if REVIT2024_OR_GREATER
                var comboSource = new Dictionary<long, string>();
                var comboTarget = new Dictionary<long, string>();
#else
                
                var comboSource = new Dictionary<int, string>();
                var comboTarget = new Dictionary<int, string>();
#endif


#if REVIT2024_OR_GREATER
                foreach (Toposolid topo in topoList)
                {
                    if (topo.HostTopoId.IsInvalid() == true)
                    {
                        try
                        {
                            var phaseParam = topo.GetParameters("Phase Created")[0]; // clsUtil.FindParameterByName(m_Topo, "Phase Created")
                            var nameParam = topo.GetParameters("Name")[0]; // clsUtil.FindParameterByName(m_Topo, "Name")

                            if ((phaseParam.AsValueString() ?? "") == (_sourcePhaseName ?? ""))
                            {
                                comboSource.Add(topo.Id.Value, "Toposolid (" + nameParam.AsString() + ") - " + phaseParam.AsValueString());
                            }

                            if ((phaseParam.AsValueString() ?? "") == (_targetPhaseName ?? ""))
                            {
                                comboTarget.Add(topo.Id.Value, "Toposolid (" + nameParam.AsString() + ") - " + phaseParam.AsValueString());
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
#else
                foreach (TopographySurface topo in topoList)
                {
                    if (topo.IsSiteSubRegion == false)
                    {
                        try
                        {
                            var phaseParam = topo.GetParameters("Phase Created")[0]; // clsUtil.FindParameterByName(m_Topo, "Phase Created")
                            var nameParam = topo.GetParameters("Name")[0]; // clsUtil.FindParameterByName(m_Topo, "Name")

                            if ((phaseParam.AsValueString() ?? "") == (_sourcePhaseName ?? ""))
                            {
                                comboSource.Add(topo.Id.IntegerValue, "Toposurface (" + nameParam.AsString() + ") - " + phaseParam.AsValueString());
                            }

                            if ((phaseParam.AsValueString() ?? "") == (_targetPhaseName ?? ""))
                            {
                                comboTarget.Add(topo.Id.IntegerValue, "Toposurface (" + nameParam.AsString() + ") - " + phaseParam.AsValueString());
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
#endif

                // load topos to source combo
                if (comboSource.Count > 0)
                {
                    frm1.cboTopoSource.DisplayMember = "Value";
                    frm1.cboTopoSource.ValueMember = "Key";
                    frm1.cboTopoSource.DataSource = new BindingSource(comboSource, null);
                    // .cboTopoSource.Items.AddRange(comboSource.Keys.ToArray)
                    frm1.cboTopoSource.SelectedIndex = 0;
                }

                // load topos to target combo
                if (comboTarget.Count > 0)
                {
                    frm1.cboTopoTarget.DisplayMember = "Value";
                    frm1.cboTopoTarget.ValueMember = "Key";
                    frm1.cboTopoTarget.DataSource = new BindingSource(comboTarget, null);
                    // .cboTopoTarget.Items.AddRange(comboTarget.Keys.ToArray)
                    frm1.cboTopoTarget.SelectedIndex = 0;
                }

                if (frm1.ShowDialog() == DialogResult.OK)
                {
                    // pick region
                    var pb = _uidoc.Selection.PickBox(PickBoxStyle.Enclosing, "Pick a rectatangle (lower left to upper right) within which the target topo points will be reset from the source topo");

                    // Dim p1 As XYZ = uidoc.Selection.PickPoint("Pick lower left corner of rectangle area to reset")
                    // Dim p2 As XYZ = uidoc.Selection.PickPoint("Pick upper right corner of rectangle area to reset")
                    double llX;
                    double llY;
                    double urX;
                    double urY;
                    if (pb.Min.X < pb.Max.X)
                    {
                        llX = pb.Min.X;
                        urX = pb.Max.X;
                    }
                    else
                    {
                        llX = pb.Max.X;
                        urX = pb.Min.X;
                    }

                    if (pb.Min.Y < pb.Max.Y)
                    {
                        llY = pb.Min.Y;
                        urY = pb.Max.Y;
                    }
                    else
                    {
                        llY = pb.Max.Y;
                        urY = pb.Min.Y;
                    }

                    var p1 = new XYZ(llX, llY, pb.Min.Z); // = pb.Min
                    var p2 = new XYZ(urX, urY, pb.Max.Z); // = pb.Max

#if REVIT2024_OR_GREATER
                    // established the bounding box of the source and target topos to get min max Z values
                    Toposolid topoSource = null;
                    Toposolid topoTarget = null;
                    foreach (Element topo in topoList)
                    {
                        if ((topo.Id.ToString() ?? "") == (frm1.cboTopoSource.SelectedValue.ToString() ?? ""))
                        {
                            topoSource = (Toposolid)topo;
                        }
                        else if ((topo.Id.ToString() ?? "") == (frm1.cboTopoTarget.SelectedValue.ToString() ?? ""))
                        {
                            topoTarget = (Toposolid)topo;
                        }
                    }
#else
                    // established the bounding box of the source and target topos to get min max Z values
                    TopographySurface topoSource = null;
                    TopographySurface topoTarget = null;
                    foreach (Element topo in topoList)
                    {
                        if ((topo.Id.ToString() ?? "") == (frm1.cboTopoSource.SelectedValue.ToString() ?? ""))
                        {
                            topoSource = (TopographySurface)topo;
                        }
                        else if ((topo.Id.ToString() ?? "") == (frm1.cboTopoTarget.SelectedValue.ToString() ?? ""))
                        {
                            topoTarget = (TopographySurface)topo;
                        }
                    }
#endif

                    if (topoSource is null | topoTarget is null)
                    {
                        return Result.Failed;
                    }

                    var v = _doc.ActiveView; // Nothing
                    var ptsExisting = new List<XYZ>();
                    var bb = topoSource.get_BoundingBox(v);


#if REVIT2024_OR_GREATER
                    var opt = new Options
                    {
                        ComputeReferences = true
                    };

                    // get points in region from topoSource
                    var min = new XYZ(p1.X, p1.Y, topoSource.get_BoundingBox(v).Min.Z - 10d);
                    var max = new XYZ(p2.X, p2.Y, topoSource.get_BoundingBox(v).Max.Z + 10d);
                    bb.Min = min;
                    bb.Max = max;

                    var m_Outline = new Outline(min, max);

                    var ptsSource = new List<XYZ>();

                    var vts = topoSource.GetSlabShapeEditor().SlabShapeVertices;
                    foreach (SlabShapeVertex shv in vts)
                    {
                        XYZ p = new XYZ(shv.Position.X, shv.Position.Y, shv.Position.Z);
                        ptsSource.Add(p);
                    }

                    foreach (XYZ pt in ptsSource)
                    {
                        if (m_Outline.Contains(pt, 0.000000001d))
                        {
                            ptsExisting.Add(pt);
                        }
                    }

                    min = new XYZ(p1.X, p1.Y, topoTarget.get_BoundingBox(v).Min.Z - 10d);
                    max = new XYZ(p2.X, p2.Y, topoTarget.get_BoundingBox(v).Max.Z + 10d);
                    bb.Min = min;
                    bb.Max = max;
                    var ptsToDelete = new List<XYZ>();
                    var outline = new Outline(min, max);

                    var ptsTarget = new List<XYZ>();

                    vts = topoTarget.GetSlabShapeEditor().SlabShapeVertices;
                    foreach (SlabShapeVertex shv in vts)
                    {
                        XYZ p = new XYZ(shv.Position.X, shv.Position.Y, shv.Position.Z);
                        ptsTarget.Add(p);
                    }

                    foreach (XYZ pt in ptsTarget)
                    {
                        if (outline.Contains(pt, 0.000000001d))
                        {
                            ptsToDelete.Add(pt);
                        }
                    }

                    if (ptsToDelete.Count > 0)
                    {
                        using (var t = new Transaction(_doc, "removing points"))
                        {
                            t.Start();

                            FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
                            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
                            t.SetFailureHandlingOptions(failureHandlingOptions);

                            //topoTarget.DeletePoints(ptsToDelete);

                            Debug.WriteLine($"Points in list {ptsTarget.Count}");
                            foreach (var p in ptsToDelete)
                            {
                                ptsTarget.Remove(p);
                            }
                            Debug.WriteLine($"Points in list after clean {ptsTarget.Count}");

                            var editor = topoTarget.GetSlabShapeEditor();
                            editor.ResetSlabShape();
                            editor.Enable();
                            editor.AddPoints(ptsTarget);

                            t.Commit();
                        }
                    }


                    // add points to topoTarget
                    if (ptsExisting.Count > 0)
                    {
                        var comparer = new XyzEqualityComparer(); // (0.01)
                        using (var t = new Transaction(_doc, "add points"))
                        {
                            t.Start();

                            FailureHandlingOptions failureHandlingOptions = t.GetFailureHandlingOptions();
                            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
                            t.SetFailureHandlingOptions(failureHandlingOptions);

                            foreach (var p in ptsExisting)
                            {
                                ptsTarget.Add(p);
                            }
                            ptsTarget = ptsTarget.Distinct(comparer).ToList();

                            topoTarget.GetSlabShapeEditor().AddPoints(ptsTarget);

                            t.Commit();
                        }
                    }
#else
                    using (var tes = new TopographyEditScope(_doc, "Reset topo"))
                    {
                        tes.Start(topoSource.Id);
                        var opt = new Options();
                        opt.ComputeReferences = true;

                        // get points in region from topoSource
                        var min = new XYZ(p1.X, p1.Y, topoSource.get_BoundingBox(v).Min.Z - 10d);
                        var max = new XYZ(p2.X, p2.Y, topoSource.get_BoundingBox(v).Max.Z + 10d);
                        bb.Min = min;
                        bb.Max = max;
                        var m_Outline = new Outline(min, max);
                        // ptsExisting = TryCast(topoSource.FindPoints(m_Outline), List(Of XYZ))

                        var pts = new List<XYZ>();
                        pts = topoSource.GetInteriorPoints() as List<XYZ>;
                        foreach (XYZ pt in pts)
                        {
                            if (m_Outline.Contains(pt, 0.000000001d))
                            {
                                ptsExisting.Add(pt);
                            }
                            // If clsUtil.BoundingBoxXyzContains(bb, pt) Then
                            // ptsExisting.Add(p)
                            // End If
                        }

                        tes.Commit(fh);
                    }

                    using (var tes = new TopographyEditScope(_doc, "Reset topo"))
                    {
                        tes.Start(topoTarget.Id);
                        var opt = new Options();
                        opt.ComputeReferences = true;
                        var min = new XYZ(p1.X, p1.Y, topoTarget.get_BoundingBox(v).Min.Z - 10d);
                        var max = new XYZ(p2.X, p2.Y, topoTarget.get_BoundingBox(v).Max.Z + 10d);
                        bb.Min = min;
                        bb.Max = max;
                        var ptsToDelete = new List<XYZ>();
                        var outline = new Outline(min, max);
                        // ptsToDelete = TryCast(topoTarget.FindPoints(m_Outline), List(Of XYZ))

                        var pts = new List<XYZ>();
                        pts = topoTarget.GetInteriorPoints() as List<XYZ>;
                        foreach (XYZ pt in pts)
                        {
                            if (outline.Contains(pt, 0.000000001d))
                            {
                                ptsToDelete.Add(pt);
                            }
                            // If clsUtil.BoundingBoxXyzContains(bb, pt) = True Then
                            // ptsToDelete.Add(p)
                            // End If
                        }

                        if (ptsToDelete.Count > 0)
                        {
                            using (var t = new Transaction(_doc, "removing points"))
                            {
                                t.Start();
                                topoTarget.DeletePoints(ptsToDelete);
                                t.Commit();
                            }
                        }
                          

                        // add points to topoTarget
                        if (ptsExisting.Count > 0)
                        {
                            var comparer = new XyzEqualityComparer(); // (0.01)
                            using (var t = new Transaction(_doc, "add points"))
                            {
                                t.Start();
                                topoTarget.AddPoints(ptsExisting.Distinct(comparer).ToList());
                                t.Commit();
                            }
                        }

                        tes.Commit(fh);
                    }
#endif
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }

        return Result.Succeeded;
    }
}
