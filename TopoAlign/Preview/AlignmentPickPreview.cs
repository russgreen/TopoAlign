using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExternalService;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Preview;

/// <summary>
/// Shows the alignment taking shape in a plan view while its points are being picked: the points picked so far and the
/// alignment laid out through them, with its curves and spirals. Create it before the first pick, call <see cref="SetPoints"/>
/// after each one and dispose it when picking ends.
/// </summary>
/// <remarks>
/// <c>PickPoint</c> reports no mouse movement and Revit offers no way to repaint a view while it waits, so the preview is redrawn
/// between picks only: there is no line that follows the cursor.
/// </remarks>
internal sealed class AlignmentPickPreview : IDisposable
{
    private static readonly ILogger log = Log.ForContext<AlignmentPickPreview>();

    /// <summary>Largest distance (feet) a drawn curve may stray from the true one.</summary>
    private const double CurveDeviation = 0.02;

    /// <summary>Size of the cross at each point, as a fraction of the width of the view.</summary>
    private const double MarkerFraction = 0.012;

    private static readonly (byte R, byte G, byte B) AlignmentColour = (0, 102, 255);
    private static readonly (byte R, byte G, byte B) TangentColour = (128, 128, 128);
    private static readonly (byte R, byte G, byte B) ProblemColour = (230, 30, 30);

    private readonly UIDocument _uidoc;
    private readonly View _view;
    private readonly UIView _uiView;
    private readonly AlignmentPreviewServer _server;
    private readonly MultiServerService _service;
    private readonly double _elevation;

    private bool _disposed;

    private AlignmentPickPreview(UIDocument uidoc, View view, UIView uiView, MultiServerService service, double elevation)
    {
        _uidoc = uidoc;
        _view = view;
        _uiView = uiView;
        _service = service;
        _elevation = elevation;
        _server = new AlignmentPreviewServer(view.Id);

        _service.AddServer(_server);
        var active = new List<Guid>(_service.GetActiveServerIds() ?? new List<Guid>()) { _server.ServerId };
        _service.SetActiveServers(active);
    }

    /// <summary>
    /// Starts a preview in the active view, or returns null when the view isn't a plan or the preview can't be set up; the
    /// caller then picks without one.
    /// </summary>
    public static AlignmentPickPreview? TryStart(UIDocument uidoc)
    {
        try
        {
            var view = uidoc.ActiveGraphicalView;
            if (view is not ViewPlan plan)
            {
                return null;
            }

            var uiView = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == view.Id);
            if (uiView is null)
            {
                return null;
            }

            if (ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) is not MultiServerService service)
            {
                return null;
            }

            return new AlignmentPickPreview(uidoc, view, uiView, service, DrawingElevation(uidoc.Document, plan));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The alignment pick preview could not be started");
            return null;
        }
    }

    /// <summary>
    /// The height to draw at: the top of the view range, so the lines sit above a toposolid or floor that is higher than the level.
    /// </summary>
    private static double DrawingElevation(Document document, ViewPlan plan)
    {
        var levelElevation = plan.GenLevel?.Elevation ?? 0.0;
        try
        {
            var range = plan.GetViewRange();
            var levelId = range.GetLevelId(PlanViewPlane.TopClipPlane);
            if (document.GetElement(levelId) is Level level)
            {
                return level.Elevation + range.GetOffset(PlanViewPlane.TopClipPlane);
            }
        }
        catch (Exception ex)
        {
            log.Debug(ex, "The view range could not be read");
        }

        return levelElevation + 100.0;
    }

    /// <summary>Sets the points picked so far and redraws. Call between picks, where the Revit API is available.</summary>
    public void SetPoints(IEnumerable<PiPoint> points)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Rebuild(points.ToList());
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The alignment pick preview could not be updated");
        }

        Refresh();
    }

    /// <summary>Shows an already laid-out alignment, such as the one a profile preview was computed for, and redraws.</summary>
    public void SetSegments(IReadOnlyList<PlanSegment> segments)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var lines = new List<PreviewLine>();
            AddPolyline(lines, PlanPolyline.FromSegments(segments, CurveDeviation), AlignmentColour);
            _server.SetLines(lines, _elevation);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The alignment preview could not be updated");
        }

        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _service.RemoveServer(_server.ServerId);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The alignment pick preview could not be removed");
        }

        _server.Release();
        Refresh();
    }

    // ---- what is drawn ------------------------------------------------------------------------------------------

    private void Rebuild(IReadOnlyList<PiPoint> points)
    {
        var lines = new List<PreviewLine>();

        // the points picked so far: tangents between them and a cross at each
        AddPolyline(lines, PlanPolyline.Through(points), TangentColour);
        foreach (var (from, to) in PlanPolyline.Crosses(points, MarkerSize()))
        {
            lines.Add(new PreviewLine(from, to, TangentColour.R, TangentColour.G, TangentColour.B));
        }

        // the alignment as it will be laid out: blue, or the plain tangents in red when the points can't be laid out
        if (points.Count >= 2)
        {
            var layout = PiGeometry.Create(points);
            if (layout.IsValid)
            {
                AddPolyline(lines, PlanPolyline.FromSegments(layout.Segments, CurveDeviation), AlignmentColour);
            }
            else
            {
                AddPolyline(lines, PlanPolyline.Through(points), ProblemColour);
            }
        }

        _server.SetLines(lines, _elevation);
    }

    private static void AddPolyline(List<PreviewLine> lines, IReadOnlyList<PlanPoint> points, (byte R, byte G, byte B) colour)
    {
        for (var i = 0; i + 1 < points.Count; i++)
        {
            lines.Add(new PreviewLine(points[i], points[i + 1], colour.R, colour.G, colour.B));
        }
    }

    /// <summary>A cross a constant fraction of the width of the view, so it stays the same size on screen as the user zooms.</summary>
    private double MarkerSize()
    {
        try
        {
            var corners = _uiView.GetZoomCorners();
            var right = _view.RightDirection;
            var d = corners[1] - corners[0];
            var width = Math.Abs(d.X * right.X + d.Y * right.Y);
            return Math.Max(width * MarkerFraction, 0.1);
        }
        catch (Exception)
        {
            return 1.0;
        }
    }

    /// <summary>
    /// Makes Revit draw the view again. Refreshing alone leaves the view as it was when nothing in the model has changed, which made
    /// the preview appear only after a pan; setting the zoom to what it already is forces a full redraw, as a pan does.
    /// </summary>
    private void Refresh()
    {
        try
        {
            _uidoc.RefreshActiveView();
            _uidoc.UpdateAllOpenViews();
        }
        catch (Exception ex)
        {
            log.Debug(ex, "The view could not be refreshed");
        }

        // switching the server off and on again makes Revit rebuild what it draws for the view
        try
        {
            var others = (_service.GetActiveServerIds() ?? new List<Guid>()).Where(id => id != _server.ServerId).ToList();
            _service.SetActiveServers(others);
            if (!_disposed)
            {
                others.Add(_server.ServerId);
                _service.SetActiveServers(others);
            }
        }
        catch (Exception ex)
        {
            log.Debug(ex, "The preview server could not be toggled");
        }

        // zooming to a view that is a hair larger and straight back is a change Revit has to draw, as a pan is
        try
        {
            var corners = _uiView.GetZoomCorners();
            var nudge = (corners[1] - corners[0]) * 1e-4;
            _uiView.ZoomAndCenterRectangle(corners[0] - nudge, corners[1] + nudge);
            _uiView.ZoomAndCenterRectangle(corners[0], corners[1]);
        }
        catch (Exception ex)
        {
            log.Debug(ex, "The view could not be redrawn");
        }
    }
}
