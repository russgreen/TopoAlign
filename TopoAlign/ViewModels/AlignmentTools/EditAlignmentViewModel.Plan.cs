using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TopoAlign.Alignments;
using TopoAlign.Preview;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Plan;
using TopoAlign.Views.Controls;

namespace TopoAlign.ViewModels.AlignmentTools;

internal partial class EditAlignmentViewModel
{
    /// <summary>Largest distance (feet) a spiral written as straights may stray from the true curve.</summary>
    private const double SpiralDeviation = 0.005;

    private const double ChangeTolerance = 1e-6;

    [ObservableProperty]
    private bool _planEditable;

    /// <summary>Why the points can't be edited, when they can't.</summary>
    [ObservableProperty]
    private string _planNote = string.Empty;

    [ObservableProperty]
    private string _pointsStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MovePointCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemovePointCommand))]
    private EditPiRow? _selectedRow;

    /// <summary>The points of intersection of the alignment, in internal coordinates.</summary>
    public ObservableCollection<EditPiRow> PiPoints { get; } = new();

    /// <summary>With <see cref="PickRequested"/>: which pick the command should run while the dialog is hidden.</summary>
    public EditAlignmentPick PendingPick { get; set; }

    /// <summary>True when the points, radii or spirals differ from the alignment as it was opened.</summary>
    public bool PlanChanged { get; private set; }

    /// <summary>True when the points as they stand can be laid out.</summary>
    public bool PlanIsValid { get; private set; } = true;

    /// <summary>What the plan graph draws; rebuilt whenever a point, radius, spiral or the units change.</summary>
    [ObservableProperty]
    private Views.Controls.PlanGraphData _graph = Views.Controls.PlanGraphData.Empty;

    /// <summary>Position of the selected row among the points, so the graph can highlight it; -1 for none.</summary>
    [ObservableProperty]
    private int _selectedPointIndex = -1;

    /// <summary>How far (feet) the graph's curves may stray from the true curve; far below a pixel at any size the dialog can show.</summary>
    private const double GraphDeviation = 0.05;

    private readonly List<PiPoint> _originalPis = new();
    private List<PlanPoint> _originalLine = new();
    private Transform _internalToShared = Transform.Identity;
    private bool _updatingRows;

    partial void OnSelectedRowChanged(EditPiRow? value)
    {
        SelectedPointIndex = value is null ? -1 : PiPoints.IndexOf(value);
    }

    // ---- loading ------------------------------------------------------------------------------------------------

    private void ClearPlan()
    {
        foreach (var row in PiPoints)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        PiPoints.Clear();
        _originalPis.Clear();
        _originalLine = new List<PlanPoint>();
        Graph = Views.Controls.PlanGraphData.Empty;
        SelectedRow = null;
        PlanEditable = false;
        PlanNote = string.Empty;
        PointsStatus = string.Empty;
        PlanChanged = false;
        PlanIsValid = true;
    }

    /// <summary>Reads the alignment's points of intersection, with their radii and spiral lengths, into the grid.</summary>
    private void LoadPlan(AlignmentJsonDocument document)
    {
        ClearPlan();

        try
        {
            if (!document.CanEditPlan(out var reason) || !PiRecovery.TryRecover(document.ReadPlan(), out var pis, out reason))
            {
                PlanNote = reason ?? "The points of this alignment can't be edited.";
                return;
            }

            var sharedToInternal = App.RevitDocument.ActiveProjectLocation.GetTotalTransform();
            _internalToShared = sharedToInternal.Inverse;

            foreach (var pi in pis)
            {
                var p = sharedToInternal.OfPoint(new XYZ(pi.X, pi.Y, 0.0));
                _originalPis.Add(new PiPoint(p.X, p.Y, pi.Radius, pi.SpiralLength));
            }

            var original = PiGeometry.Create(_originalPis);
            if (original.IsValid)
            {
                _originalLine = PlanPolyline.FromSegments(original.Segments, GraphDeviation);
            }

            PlanEditable = true;
            RestoreOriginalPoints();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "The alignment's points could not be read");
            ClearPlan();
            PlanNote = "The points of this alignment could not be read: " + ex.Message;
        }
    }

    private void RestoreOriginalPoints()
    {
        foreach (var row in PiPoints)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        PiPoints.Clear();
        foreach (var pi in _originalPis)
        {
            AddRow(new EditPiRow(pi.X, pi.Y, pi.Radius, pi.SpiralLength), PiPoints.Count);
        }

        RefreshPlan();
    }

    private void AddRow(EditPiRow row, int index)
    {
        SetDisplay(row);
        row.PropertyChanged += OnRowChanged;
        PiPoints.Insert(index, row);
    }

    // ---- units and editing --------------------------------------------------------------------------------------

    private void SetDisplay(EditPiRow row)
    {
        if (SelectedDisplayUnit is null)
        {
            return;
        }

        var wasUpdating = _updatingRows;
        _updatingRows = true;
        var unit = SelectedDisplayUnit.DisplayUnit;
        row.X = Round(UnitUtils.ConvertFromInternalUnits(row.XFeet, unit));
        row.Y = Round(UnitUtils.ConvertFromInternalUnits(row.YFeet, unit));
        row.Radius = Round(UnitUtils.ConvertFromInternalUnits(row.RadiusFeet, unit));
        row.SpiralLength = Round(UnitUtils.ConvertFromInternalUnits(row.SpiralFeet, unit));
        _updatingRows = wasUpdating;
    }

    /// <summary>Shows every point in the dialog's current units.</summary>
    private void RefreshRowDisplays()
    {
        foreach (var row in PiPoints)
        {
            SetDisplay(row);
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingRows || sender is not EditPiRow row)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EditPiRow.X):
                row.XFeet = ToInternal(row.X);
                break;

            case nameof(EditPiRow.Y):
                row.YFeet = ToInternal(row.Y);
                break;

            case nameof(EditPiRow.Radius):
                row.RadiusFeet = Math.Max(ToInternal(row.Radius), 0.0);
                break;

            case nameof(EditPiRow.SpiralLength):
                row.SpiralFeet = Math.Max(ToInternal(row.SpiralLength), 0.0);
                break;

            default:
                return;
        }

        RefreshPlan();
    }

    /// <summary>Numbers the points, lays them out, and updates the status, the changed flag and the Apply button.</summary>
    private void RefreshPlan()
    {
        for (var i = 0; i < PiPoints.Count; i++)
        {
            var row = PiPoints[i];
            row.Number = i + 1;
            row.IsEnd = i == 0 || i == PiPoints.Count - 1;

            // the ends of the alignment have no curve
            if (row.IsEnd && (row.RadiusFeet != 0.0 || row.SpiralFeet != 0.0))
            {
                row.RadiusFeet = 0.0;
                row.SpiralFeet = 0.0;
                SetDisplay(row);
            }
        }

        PlanChanged = PlanEditable && DiffersFromOriginal();
        SelectedPointIndex = SelectedRow is null ? -1 : PiPoints.IndexOf(SelectedRow);

        List<PlanPoint>? edited = null;
        if (!PlanEditable || SelectedDisplayUnit is null)
        {
            PlanIsValid = true;
            PointsStatus = string.Empty;
        }
        else if (PiPoints.Count < 2)
        {
            PlanIsValid = false;
            PointsStatus = "An alignment needs at least two points.";
        }
        else
        {
            var layout = PiGeometry.Create(CurrentPiPoints());
            PlanIsValid = layout.IsValid;
            if (layout.IsValid)
            {
                var length = UnitUtils.ConvertFromInternalUnits(PlanSegment.TotalLength(layout.Segments), SelectedDisplayUnit.DisplayUnit);
                PointsStatus = $"{PiPoints.Count} points, {length:N2} {SelectedDisplayUnit.Label}" + (PlanChanged ? " (edited)" : string.Empty);
                edited = PlanPolyline.FromSegments(layout.Segments, GraphDeviation);
            }
            else
            {
                PointsStatus = layout.Errors[0];
            }
        }

        UpdateGraph(edited);
        RemovePointCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Rebuilds the plan graph in the dialog's units. <paramref name="edited"/> is null when the points can't be laid out.</summary>
    private void UpdateGraph(IReadOnlyList<PlanPoint>? edited)
    {
        if (!PlanEditable || SelectedDisplayUnit is null || PiPoints.Count == 0)
        {
            Graph = Views.Controls.PlanGraphData.Empty;
            return;
        }

        var unit = SelectedDisplayUnit.DisplayUnit;
        System.Windows.Point ToDisplay(double x, double y) => new(
            UnitUtils.ConvertFromInternalUnits(x, unit),
            UnitUtils.ConvertFromInternalUnits(y, unit));

        var points = PiPoints.Select(row => ToDisplay(row.XFeet, row.YFeet)).ToList();
        Graph = new Views.Controls.PlanGraphData(
            edited?.Select(p => ToDisplay(p.X, p.Y)).ToList() ?? new List<System.Windows.Point>(),
            _originalLine.Select(p => ToDisplay(p.X, p.Y)).ToList(),
            points,
            points,
            SelectedDisplayUnit.Label);
    }

    private bool DiffersFromOriginal()
    {
        if (PiPoints.Count != _originalPis.Count)
        {
            return true;
        }

        for (var i = 0; i < PiPoints.Count; i++)
        {
            var row = PiPoints[i];
            var original = _originalPis[i];
            if (Math.Abs(row.XFeet - original.X) > ChangeTolerance || Math.Abs(row.YFeet - original.Y) > ChangeTolerance ||
                Math.Abs(row.RadiusFeet - original.Radius) > ChangeTolerance || Math.Abs(row.SpiralFeet - original.SpiralLength) > ChangeTolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The points in feet (internal units), ready to lay out.</summary>
    private List<PiPoint> CurrentPiPoints()
    {
        return PiPoints.Select(row => new PiPoint(row.XFeet, row.YFeet, row.RadiusFeet, row.SpiralFeet)).ToList();
    }

    // ---- commands -----------------------------------------------------------------------------------------------

    private bool HasSelectedPoint() => SelectedRow is not null && PiPoints.Contains(SelectedRow);

    private bool CanRemovePoint() => HasSelectedPoint() && PiPoints.Count > 2;

    [RelayCommand(CanExecute = nameof(HasSelectedPoint))]
    private void MovePoint()
    {
        PendingPick = EditAlignmentPick.MovePoint;
        PickRequested = true;
        OnHideRequested();
    }

    [RelayCommand]
    private void AddPoint()
    {
        PendingPick = EditAlignmentPick.AddPoint;
        PickRequested = true;
        OnHideRequested();
    }

    [RelayCommand(CanExecute = nameof(CanRemovePoint))]
    private void RemovePoint()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        row.PropertyChanged -= OnRowChanged;
        PiPoints.Remove(row);
        SelectedRow = null;
        RefreshPlan();
    }

    [RelayCommand]
    private void ResetPoints()
    {
        if (PlanEditable)
        {
            RestoreOriginalPoints();
        }
    }

    partial void OnPlanEditableChanged(bool value) => OnPropertyChanged(nameof(PlanNotEditable));

    /// <summary>True when an alignment is chosen but its points can't be edited.</summary>
    public bool PlanNotEditable => HasAlignment && !PlanEditable;

    // ---- picking ------------------------------------------------------------------------------------------------

    /// <summary>Runs the pick the dialog was hidden for. Called by the command while the dialog is hidden.</summary>
    internal void RunPendingPick()
    {
        var kind = PendingPick;
        PendingPick = EditAlignmentPick.None;

        switch (kind)
        {
            case EditAlignmentPick.MovePoint:
                PickPointPosition(add: false);
                break;

            case EditAlignmentPick.AddPoint:
                PickPointPosition(add: true);
                break;

            default:
                PickAlignmentElement();
                break;
        }
    }

    /// <summary>Picks a position in plan: the new place for the selected point, or a new point on the alignment.</summary>
    private void PickPointPosition(bool add)
    {
        AlignmentPickPreview? preview = null;
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints |
                        ObjectSnapTypes.Centers | ObjectSnapTypes.Points | ObjectSnapTypes.Nearest;

            // in a plan view the alignment and its points are drawn while the position is picked
            preview = AlignmentPickPreview.TryStart(uidoc);
            preview?.SetPoints(CurrentPiPoints());

            var target = add ? null : SelectedRow;
            var prompt = add
                ? "Pick the position of the new point"
                : $"Pick the new position of point {SelectedRow?.Number}";
            var point = uidoc.Selection.PickPoint(snaps, prompt);

            if (add)
            {
                InsertPoint(point.X, point.Y);
            }
            else if (target is not null)
            {
                target.XFeet = point.X;
                target.YFeet = point.Y;
                SetDisplay(target);
                RefreshPlan();
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Esc leaves the point where it was
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while picking a point position");
            TaskDialog.Show("Edit Alignment", ex.Message);
        }
        finally
        {
            preview?.Dispose();
        }
    }

    /// <summary>Adds a sharp point at the given position, on the tangent nearest to it, or beyond an end of the alignment.</summary>
    private void InsertPoint(double x, double y)
    {
        var index = InsertIndex(x, y);
        var row = new EditPiRow(x, y, 0.0, 0.0);
        AddRow(row, index);
        SelectedRow = row;
        RefreshPlan();
    }

    private int InsertIndex(double x, double y)
    {
        var count = PiPoints.Count;
        if (count < 2)
        {
            return count;
        }

        var best = 0;
        var bestDistance = double.MaxValue;
        var bestT = 0.0;
        for (var i = 0; i + 1 < count; i++)
        {
            var a = PiPoints[i];
            var b = PiPoints[i + 1];
            var dx = b.XFeet - a.XFeet;
            var dy = b.YFeet - a.YFeet;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared <= 1e-12 ? 0.0 : ((x - a.XFeet) * dx + (y - a.YFeet) * dy) / lengthSquared;
            var clamped = Math.Max(0.0, Math.Min(1.0, t));
            var distance = PlanPoint.Distance(new PlanPoint(x, y), new PlanPoint(a.XFeet + dx * clamped, a.YFeet + dy * clamped));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
                bestT = t;
            }
        }

        if (best == 0 && bestT < 0.0)
        {
            return 0;
        }

        if (best == count - 2 && bestT > 1.0)
        {
            return count;
        }

        return best + 1;
    }

    // ---- applying -----------------------------------------------------------------------------------------------

    /// <summary>Lays the edited points out and writes the resulting plan, and the profile that follows it, into the document.</summary>
    private void ApplyPlan(AlignmentJsonDocument document)
    {
        // the document is in shared coordinates; a move, rotation or shift of the points doesn't change how they lay out
        var shared = PiPoints.Select(row =>
        {
            var p = _internalToShared.OfPoint(new XYZ(row.XFeet, row.YFeet, 0.0));
            return new PiPoint(p.X, p.Y, row.RadiusFeet, row.SpiralFeet);
        }).ToList();

        var layout = PiGeometry.Create(shared);
        if (!layout.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, layout.Errors));
        }

        document.SetPlan(layout.Segments, SpiralDeviation);
    }
}