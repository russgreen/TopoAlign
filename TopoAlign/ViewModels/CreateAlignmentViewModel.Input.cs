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
using TopoAlign.Alignments.Plan;
using TopoAlign.PickFilters;

namespace TopoAlign.ViewModels;

internal partial class CreateAlignmentViewModel
{
    /// <summary>Largest distance (feet) a spiral written as straights may stray from the true curve.</summary>
    private const double SpiralDeviation = 0.005;

    [ObservableProperty]
    private AlignmentInputSource _inputSource = AlignmentInputSource.Lines;

    [ObservableProperty]
    private bool _addSpirals;

    [ObservableProperty]
    private decimal _spiralLength;

    [ObservableProperty]
    private decimal _defaultRadius;

    [ObservableProperty]
    private string _pointsStatus = "No points picked.";

    /// <summary>The picked points of intersection (point input only).</summary>
    public ObservableCollection<PiRow> PiPoints { get; } = new();

    /// <summary>With <see cref="CreateAlignmentViewModel.PickRequested"/>: the requested pick is points in plan.</summary>
    public bool PickPointsRequested { get; set; }

    /// <summary>With <see cref="CreateAlignmentViewModel.PickRequested"/>: the requested pick is CAD curves.</summary>
    public bool PickCadRequested { get; set; }

    private bool _pointsAreValid;

    public bool IsLinesInput
    {
        get => InputSource == AlignmentInputSource.Lines;
        set
        {
            if (value)
            {
                InputSource = AlignmentInputSource.Lines;
            }
        }
    }

    public bool IsPointsInput
    {
        get => InputSource == AlignmentInputSource.Points;
        set
        {
            if (value)
            {
                InputSource = AlignmentInputSource.Points;
            }
        }
    }

    public bool IsCadInput
    {
        get => InputSource == AlignmentInputSource.Cad;
        set
        {
            if (value)
            {
                InputSource = AlignmentInputSource.Cad;
            }
        }
    }

    /// <summary>True when the geometry comes from picked curves (lines or CAD) rather than points.</summary>
    public bool IsCurveInput => InputSource != AlignmentInputSource.Points;

    partial void OnInputSourceChanged(AlignmentInputSource value)
    {
        OnPropertyChanged(nameof(IsLinesInput));
        OnPropertyChanged(nameof(IsPointsInput));
        OnPropertyChanged(nameof(IsCadInput));
        OnPropertyChanged(nameof(IsCurveInput));
        ProfilePreview = string.Empty;
        CreateAlignmentCommand.NotifyCanExecuteChanged();
    }

    partial void OnAddSpiralsChanged(bool value) => ProfilePreview = string.Empty;

    partial void OnSpiralLengthChanged(decimal value) => ProfilePreview = string.Empty;

    /// <summary>True when the current input has plan geometry that can be turned into an alignment.</summary>
    private bool HasGeometry => InputSource == AlignmentInputSource.Points
        ? _pointsAreValid
        : IsChainContinuous && _orderedCurves.Count > 0;

    // ---- picking ------------------------------------------------------------------------------------------------

    private void ResetPickKind()
    {
        PreviewLayoutRequested = false;
        PickToposolidRequested = false;
        PickPointsRequested = false;
        PickCadRequested = false;
    }

    [RelayCommand]
    private void PickPoints()
    {
        ResetPickKind();
        PickRequested = true;
        PickPointsRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void PickCad()
    {
        ResetPickKind();
        PickRequested = true;
        PickCadRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RemoveLastPoint()
    {
        if (PiPoints.Count > 0)
        {
            RemovePoint(PiPoints[PiPoints.Count - 1]);
        }
    }

    [RelayCommand]
    private void ClearPoints()
    {
        foreach (var row in PiPoints.ToList())
        {
            row.PropertyChanged -= OnPointChanged;
        }

        PiPoints.Clear();
        RefreshPoints();
    }

    /// <summary>Picks points in plan until the user presses Esc. Called by the command while the dialog is hidden.</summary>
    internal void PickPointsInPlan()
    {
        AlignmentPickPreview? preview = null;
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var snaps = ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Midpoints |
                        ObjectSnapTypes.Centers | ObjectSnapTypes.Points | ObjectSnapTypes.Nearest;

            // in a plan view the alignment is drawn as it takes shape, with a rubber band to the cursor
            preview = AlignmentPickPreview.TryStart(uidoc);
            preview?.SetPoints(CurrentPiPoints());

            while (true)
            {
                var point = uidoc.Selection.PickPoint(snaps, $"Pick point {PiPoints.Count + 1} of the alignment (Esc when finished)");
                AddPoint(point.X, point.Y, point.Z);
                preview?.SetPoints(CurrentPiPoints());
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Esc ends the list of points
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while picking alignment points");
            TaskDialog.Show("Create Alignment", ex.Message);
        }
        finally
        {
            preview?.Dispose();
        }
    }

    /// <summary>Picks lines, arcs and polylines on a CAD drawing and chains them. Called by the command while the dialog is hidden.</summary>
    internal void PickCadCurves()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var picked = uidoc.Selection.PickObjects(ObjectType.PointOnElement, new CadCurvePickFilter(),
                "Select lines, arcs or polylines on the CAD drawing, then click Finish");

            var curves = CadCurveReader.Read(App.RevitDocument, picked, out var error);
            _selectedCurveIds.Clear();
            if (error is not null)
            {
                _orderedCurves.Clear();
                IsChainContinuous = false;
                SelectedCurveCount = 0;
                ChainStatus = error;
                return;
            }

            ApplyCurveChain(curves);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while reading CAD curves");
            TaskDialog.Show("Create Alignment", ex.Message);
        }
    }

    /// <summary>Adds a point of intersection at the given position (feet), using the default radius and spiral length.</summary>
    internal void AddPoint(double x, double y, double z)
    {
        var last = PiPoints.Count > 0 ? PiPoints[PiPoints.Count - 1] : null;
        if (last is not null && Math.Abs(last.X - x) < 1e-4 && Math.Abs(last.Y - y) < 1e-4)
        {
            return;
        }

        var row = new PiRow(x, y, z, DefaultRadius, AddSpirals ? SpiralLength : 0m);
        row.PropertyChanged += OnPointChanged;
        PiPoints.Add(row);
        RefreshPoints();
    }

    private void RemovePoint(PiRow row)
    {
        row.PropertyChanged -= OnPointChanged;
        PiPoints.Remove(row);
        RefreshPoints();
    }

    private void OnPointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PiRow.Radius) or nameof(PiRow.SpiralLength))
        {
            RefreshPoints();
        }
    }

    // ---- laying the points out ----------------------------------------------------------------------------------

    /// <summary>The picked points in feet, with each radius and spiral length converted from display units.</summary>
    private List<PiPoint> CurrentPiPoints()
    {
        return PiPoints
            .Select(row => new PiPoint(row.X, row.Y, Math.Max(ToInternal(row.Radius), 0.0), Math.Max(ToInternal(row.SpiralLength), 0.0)))
            .ToList();
    }

    private PiResult LayoutPoints(bool reversed)
    {
        var pis = CurrentPiPoints();

        if (reversed)
        {
            pis.Reverse();
        }

        return PiGeometry.Create(pis);
    }

    /// <summary>Renumbers the points, lays them out and refreshes the status line and the Create button.</summary>
    private void RefreshPoints()
    {
        for (var i = 0; i < PiPoints.Count; i++)
        {
            PiPoints[i].Number = i + 1;
            if (SelectedDisplayUnit is not null)
            {
                var unit = SelectedDisplayUnit.DisplayUnit;
                PiPoints[i].Location = $"{UnitUtils.ConvertFromInternalUnits(PiPoints[i].X, unit):N2}, {UnitUtils.ConvertFromInternalUnits(PiPoints[i].Y, unit):N2}";
            }
        }

        ProfilePreview = string.Empty;

        if (PiPoints.Count == 0)
        {
            _pointsAreValid = false;
            PointsStatus = "No points picked.";
        }
        else if (PiPoints.Count == 1 || SelectedDisplayUnit is null)
        {
            _pointsAreValid = false;
            PointsStatus = "Pick at least two points.";
        }
        else
        {
            var result = LayoutPoints(reversed: false);
            _pointsAreValid = result.IsValid;
            if (result.IsValid)
            {
                var length = UnitUtils.ConvertFromInternalUnits(PlanSegment.TotalLength(result.Segments), SelectedDisplayUnit.DisplayUnit);
                PointsStatus = $"{PiPoints.Count} points, {result.Segments.Count} segments, {length:N2} {SelectedDisplayUnit.Label}";
            }
            else
            {
                PointsStatus = result.Errors[0];
            }
        }

        CreateAlignmentCommand.NotifyCanExecuteChanged();
    }

    // ---- building the layout for preview and create ------------------------------------------------------------

    /// <summary>Builds the plan geometry. Throws with a user-readable message when it can't be built.</summary>
    private PlanLayout BuildLayout()
    {
        if (InputSource == AlignmentInputSource.Points)
        {
            if (PiPoints.Count < 2)
            {
                throw new InvalidOperationException("Pick at least two points.");
            }

            var laidOut = LayoutPoints(ReverseDirection);
            if (!laidOut.IsValid)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, laidOut.Errors));
            }

            return new PlanLayout(null, laidOut.Segments, PiPoints[0].Z);
        }

        if (_orderedCurves.Count == 0)
        {
            throw new InvalidOperationException("Pick lines first.");
        }

        var curves = ReverseDirection
            ? CurveChainer.ReverseChain(_orderedCurves)
            : _orderedCurves.ToList();
        var elevation = curves[0].GetEndPoint(0).Z;
        var segments = PlanSegmentAdapter.FromCurves(curves);

        if (!AddSpirals)
        {
            return new PlanLayout(curves, segments, elevation);
        }

        var spiralLength = ToInternal(SpiralLength);
        if (spiralLength <= 0.0)
        {
            throw new InvalidOperationException("Enter a spiral length greater than zero, or untick the transition spirals.");
        }

        // spirals go where they fit; curves that can't take them are kept as drawn and reported
        var spiralled = PiGeometry.AddSpirals(segments, spiralLength);
        var notice = SpiralNotice(spiralled);
        if (spiralled.SpiralledCurves == 0)
        {
            return new PlanLayout(curves, segments, elevation, notice);
        }

        return new PlanLayout(null, spiralled.Segments, elevation, notice);
    }

    private static string SpiralNotice(SpiralResult result)
    {
        if (result.Skipped.Count == 0)
        {
            return string.Empty;
        }

        var text = result.SpiralledCurves == 0
            ? "No spirals were added."
            : $"Spirals were added to {result.SpiralledCurves} curve{(result.SpiralledCurves == 1 ? string.Empty : "s")}.";
        return text + Environment.NewLine + string.Join(Environment.NewLine, result.Skipped);
    }

    private void LoadInputSettings()
    {
        AddSpirals = _settings.CreateAlignmentAddSpirals;
        SpiralLength = ToDisplay(_settings.CreateAlignmentSpiralLength);
        DefaultRadius = ToDisplay(_settings.CreateAlignmentDefaultRadius);
    }

    private void SaveInputSettings()
    {
        _settings.CreateAlignmentAddSpirals = AddSpirals;
        _settings.CreateAlignmentSpiralLength = (decimal)ToInternal(SpiralLength);
        _settings.CreateAlignmentDefaultRadius = (decimal)ToInternal(DefaultRadius);
    }
}