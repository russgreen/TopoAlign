using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Preview;

namespace TopoAlign.ViewModels;

internal sealed class CreateAlignmentRequest
{
    public CreateAlignmentRequest(string name, string json, double planLength, IList<ElementId>? sourceCurveIdsToDelete, string notice = "")
    {
        Name = name;
        Json = json;
        PlanLength = planLength;
        SourceCurveIdsToDelete = sourceCurveIdsToDelete;
        Notice = notice;
    }

    /// <summary>Extra text for the summary, for example curves that could not take spirals.</summary>
    public string Notice { get; }

    public string Name { get; }
    public string Json { get; }

    /// <summary>Alignment length in feet (internal units).</summary>
    public double PlanLength { get; }
    public IList<ElementId>? SourceCurveIdsToDelete { get; }
}

internal partial class CreateAlignmentViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<CreateAlignmentViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlignmentCommand))]
    private string _alignmentName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlignmentCommand))]
    private decimal _startStation;

    [ObservableProperty]
    private bool _reverseDirection;

    [ObservableProperty]
    private bool _deleteSourceLines;

    [ObservableProperty]
    private string _chainStatus = "No lines selected.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlignmentCommand))]
    private bool _isChainContinuous;

    [ObservableProperty]
    private int _selectedCurveCount;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlignmentCommand))]
    private bool _drapeOnTopo;

    [ObservableProperty]
    private string _topoStatus = "No toposolid selected.";

    [ObservableProperty]
    private decimal _sampleInterval;

    [ObservableProperty]
    private decimal _verticalTolerance;

    [ObservableProperty]
    private decimal _elevationOffset;

    [ObservableProperty]
    private decimal _minVerticalCurveLength;

    [ObservableProperty]
    private string _profilePreview = string.Empty;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>True when the dialog was hidden to pick lines; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>With <see cref="PickRequested"/>: the requested pick is the toposolid rather than the lines.</summary>
    public bool PickToposolidRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public CreateAlignmentRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly List<ElementId> _selectedCurveIds = new();
    private List<Curve> _orderedCurves = new();
    private Element? _topography;

#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits;
#else
    private ForgeTypeId _docDisplayUnits;
#endif

    public CreateAlignmentViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Create Alignment {informationVersion}";

#if REVIT2018 || REVIT2019 || REVIT2020
        _docDisplayUnits = App.RevitDocument.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        _docDisplayUnits = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif

        PopulateDisplayUnitList();
        LoadSettings();
        SetDefaultName();
    }

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel value)
    {
        if (oldValue is not null)
        {
            StartStation = Helpers.UnitConversion.Convert(StartStation, oldValue.DisplayUnit, value.DisplayUnit);
            SampleInterval = Helpers.UnitConversion.Convert(SampleInterval, oldValue.DisplayUnit, value.DisplayUnit);
            VerticalTolerance = Helpers.UnitConversion.Convert(VerticalTolerance, oldValue.DisplayUnit, value.DisplayUnit);
            ElevationOffset = Helpers.UnitConversion.Convert(ElevationOffset, oldValue.DisplayUnit, value.DisplayUnit);
            MinVerticalCurveLength = Helpers.UnitConversion.Convert(MinVerticalCurveLength, oldValue.DisplayUnit, value.DisplayUnit);
            SpiralLength = Helpers.UnitConversion.Convert(SpiralLength, oldValue.DisplayUnit, value.DisplayUnit);
            DefaultRadius = Helpers.UnitConversion.Convert(DefaultRadius, oldValue.DisplayUnit, value.DisplayUnit);
            foreach (var row in PiPoints)
            {
                row.Radius = Helpers.UnitConversion.Convert(row.Radius, oldValue.DisplayUnit, value.DisplayUnit);
                row.SpiralLength = Helpers.UnitConversion.Convert(row.SpiralLength, oldValue.DisplayUnit, value.DisplayUnit);
            }
        }

        UpdateChainStatus();
        RefreshPoints();
    }

    // any change to what the profile depends on makes the last preview stale
    partial void OnReverseDirectionChanged(bool value) => ProfilePreview = string.Empty;
    partial void OnDrapeOnTopoChanged(bool value) => ProfilePreview = string.Empty;
    partial void OnSampleIntervalChanged(decimal value) => ProfilePreview = string.Empty;
    partial void OnVerticalToleranceChanged(decimal value) => ProfilePreview = string.Empty;
    partial void OnElevationOffsetChanged(decimal value) => ProfilePreview = string.Empty;
    partial void OnMinVerticalCurveLengthChanged(decimal value) => ProfilePreview = string.Empty;

    private bool CanCreateAlignment()
    {
        return HasGeometry &&
               !string.IsNullOrWhiteSpace(AlignmentName) &&
               (!DrapeOnTopo || _topography is not null);
    }

    [RelayCommand]
    private void PickLines()
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        ResetPickKind();
        PickRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void PickToposolid()
    {
        ResetPickKind();
        PickRequested = true;
        PickToposolidRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs whichever pick the dialog asked for. Called by the command while the dialog is hidden.</summary>
    internal void RunPendingPick()
    {
        if (PreviewLayoutRequested)
        {
            PreviewLayoutRequested = false;
            ShowLayoutInPlan();
        }
        else if (PickToposolidRequested)
        {
            PickToposolidElement();
        }
        else if (PickPointsRequested)
        {
            PickPointsInPlan();
        }
        else if (PickCadRequested)
        {
            PickCadCurves();
        }
        else
        {
            PickCurves();
        }
    }

    /// <summary>Selects the toposolid (pre-selection or interactive pick) the profile is draped on.</summary>
    internal void PickToposolidElement()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var filter = new TopoPickFilter();

            Element? topography = null;
            foreach (var id in uidoc.Selection.GetElementIds())
            {
                var element = App.RevitDocument.GetElement(id);
                if (filter.AllowElement(element))
                {
                    topography = element;
                    break;
                }
            }

            if (topography is null)
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, filter, "Select the toposolid to drape the profile on");
                topography = App.RevitDocument.GetElement(reference);
            }

            _topography = topography;
#if REVIT2024_OR_GREATER
            var topographyId = topography.Id.Value;
#else
            var topographyId = topography.Id.IntegerValue;
#endif
            TopoStatus = $"{topography.Category?.Name ?? "Topography"} (Id {topographyId})";
            ProfilePreview = string.Empty;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting the toposolid");
            TaskDialog.Show("Create Alignment", ex.Message);
        }
        finally
        {
            CreateAlignmentCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Selects the curves (pre-selection or interactive pick) and chains them. Called by the command.</summary>
    internal void PickCurves()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var filter = new CurvePickFilter();

            _selectedCurveIds.Clear();
            var curves = new List<Curve>();

            foreach (var id in uidoc.Selection.GetElementIds())
            {
                var element = App.RevitDocument.GetElement(id);
                if (!filter.AllowElement(element))
                {
                    continue;
                }

                if (element is CurveElement curveElement)
                {
                    _selectedCurveIds.Add(element.Id);
                    curves.Add(curveElement.GeometryCurve);
                }
            }

            if (curves.Count == 0)
            {
                var picked = uidoc.Selection.PickObjects(ObjectType.Element, filter, "Select model/detail lines and arcs for the alignment");
                foreach (var reference in picked)
                {
                    var element = App.RevitDocument.GetElement(reference);
                    if (element is CurveElement curveElement)
                    {
                        _selectedCurveIds.Add(element.Id);
                        curves.Add(curveElement.GeometryCurve);
                    }
                }
            }

            ApplyCurveChain(curves);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting curves for alignment creation");
            TaskDialog.Show("Create Alignment", ex.Message);
        }
    }

    /// <summary>Chains the curves into one continuous run and updates the status; shared by the line and CAD picks.</summary>
    private void ApplyCurveChain(List<Curve> curves)
    {
        var chainResult = CurveChainer.TryChain(curves);
        if (!chainResult.IsContinuous)
        {
            _orderedCurves.Clear();
            IsChainContinuous = false;
            SelectedCurveCount = 0;
            ChainStatus = chainResult.ErrorMessage ?? "Selected curves are not continuous.";
            return;
        }

        _orderedCurves = chainResult.Curves;
        IsChainContinuous = true;
        SelectedCurveCount = _orderedCurves.Count;

        var displayLength = UnitUtils.ConvertFromInternalUnits(chainResult.TotalLength, SelectedDisplayUnit.DisplayUnit);
        ChainStatus = $"{SelectedCurveCount} lines, continuous, {displayLength:N2} {SelectedDisplayUnit.Label}";
    }

    /// <summary>The draped profile for the current chain: simplified PVIs plus the vertical curve length to apply.</summary>
    private sealed class DrapedProfile
    {
        public DrapedProfile(List<ProfilePoint> pvis, int missingCount, double minVerticalCurveLength)
        {
            Pvis = pvis;
            MissingCount = missingCount;
            MinVerticalCurveLength = minVerticalCurveLength;
        }

        public List<ProfilePoint> Pvis { get; }
        public int MissingCount { get; }
        public double MinVerticalCurveLength { get; }
    }

    private double ToInternal(decimal displayValue)
    {
        return ((double)displayValue).FromUnit(SelectedDisplayUnit.DisplayUnit);
    }

    /// <summary>Samples the toposolid along the chain and reduces it to PVIs. Throws with a user-readable message on bad input.</summary>
    private DrapedProfile ComputeProfile(IReadOnlyList<PlanSegment> segments)
    {
        if (_topography is null)
        {
            throw new InvalidOperationException("Pick a toposolid to drape the profile on.");
        }

        var interval = ToInternal(SampleInterval);
        if (interval <= 0.0)
        {
            throw new InvalidOperationException("The sample interval must be greater than zero.");
        }

        var tolerance = ToInternal(VerticalTolerance);
        if (tolerance < 0.0)
        {
            throw new InvalidOperationException("The vertical tolerance can't be negative.");
        }

        var minCurve = ToInternal(MinVerticalCurveLength);
        if (minCurve < 0.0)
        {
            throw new InvalidOperationException("The minimum vertical curve length can't be negative.");
        }

        var surface = TopographyReader.Read(_topography);
        var sampled = ProfileSampler.Sample(segments, surface, interval, ToInternal(ElevationOffset));
        var pvis = ProfileSimplifier.Simplify(sampled.Samples, tolerance);

        return new DrapedProfile(pvis, sampled.MissingCount, minCurve);
    }

    [RelayCommand]
    private void PreviewProfile()
    {
        try
        {
            var layout = BuildLayout();
            var profile = ComputeProfile(layout.Segments);
            var unit = SelectedDisplayUnit.DisplayUnit;
            var minElevation = UnitUtils.ConvertFromInternalUnits(profile.Pvis.Min(p => p.Elevation), unit);
            var maxElevation = UnitUtils.ConvertFromInternalUnits(profile.Pvis.Max(p => p.Elevation), unit);
            var maxGrade = ProfileSimplifier.MaxAbsGrade(profile.Pvis);

            var text = $"{profile.Pvis.Count} PVIs, elevation {minElevation:N2} to {maxElevation:N2} {SelectedDisplayUnit.Label}, max grade {maxGrade:P1}";
            if (profile.MissingCount > 0)
            {
                text += $". {profile.MissingCount} stations are off the toposolid and were bridged.";
            }

            ProfilePreview = text;

            // Revit doesn't redraw its view under a modal dialog, so hide the dialog and show the alignment in the plan view
            // while the user looks at it; the command shows the dialog again afterwards
            ResetPickKind();
            PickRequested = true;
            PreviewLayoutRequested = true;
            _previewSegments = layout.Segments;
            HideRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Profile preview failed");
            ProfilePreview = ex.Message;
        }
    }

    /// <summary>With <see cref="PickRequested"/>: show the laid-out alignment in the plan view rather than pick anything.</summary>
    public bool PreviewLayoutRequested { get; set; }

    private IReadOnlyList<PlanSegment>? _previewSegments;

    /// <summary>
    /// Draws the alignment the profile preview was computed for as temporary lines in the plan view, and holds it there until the
    /// user presses Esc or clicks. Called by the command while the dialog is hidden.
    /// </summary>
    internal void ShowLayoutInPlan()
    {
        var segments = _previewSegments;
        _previewSegments = null;
        if (segments is null || segments.Count == 0)
        {
            return;
        }

        AlignmentPickPreview? preview = null;
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            preview = AlignmentPickPreview.TryStart(uidoc);
            preview?.SetSegments(segments);
            uidoc.UpdateAllOpenViews();
            uidoc.Selection.PickPoint("Previewing the alignment. Click or press Esc to return to the dialog");
            
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Esc returns to the dialog
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Showing the alignment preview failed");
        }
        finally
        {
            preview?.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateAlignment))]
    private void CreateAlignment()
    {
        try
        {
            if (InputSource != AlignmentInputSource.Points && _orderedCurves.Count == 0)
            {
                TaskDialog.Show("Create Alignment", "Pick lines first.");
                return;
            }

            if (!AlignmentApi.IsAvailable)
            {
                TaskDialog.Show("Create Alignment", "The Revit Civil Alignments add-in was not found, so alignments cannot be created in this version of Revit.");
                return;
            }

            var layout = BuildLayout();

            var startStationInternal = ((double)StartStation).FromUnit(SelectedDisplayUnit.DisplayUnit);

            // Alignment JSON is written in shared coordinates; the profile elevation takes the same offset.
            var internalToShared = App.RevitDocument.ActiveProjectLocation.GetTotalTransform().Inverse;
            double planLength;
            string json;
            var profile = DrapeOnTopo ? ComputeProfile(layout.Segments) : null;
            if (layout.Curves is not null)
            {
                // the picked lines and arcs are used exactly as drawn
                json = profile is null
                    ? AlignmentJsonBuilder.Build(AlignmentName, layout.Curves, internalToShared, startStationInternal, out planLength)
                    : AlignmentJsonBuilder.Build(AlignmentName, layout.Curves, internalToShared, startStationInternal, profile.Pvis, profile.MinVerticalCurveLength, out planLength);
            }
            else
            {
                json = AlignmentJsonBuilder.BuildFromSegments(AlignmentName, layout.Segments, internalToShared, startStationInternal, layout.FlatElevation,
                    profile?.Pvis, profile?.MinVerticalCurveLength ?? 0.0, SpiralDeviation, out planLength);
            }

            // The alignment is created by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new CreateAlignmentRequest(
                AlignmentName,
                json,
                planLength,
                DeleteSourceLines ? _selectedCurveIds.ToList() : null,
                layout.Notice);

            SaveSettings(startStationInternal);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Create alignment failed");
            TaskDialog.Show("Create Alignment", ex.Message);
        }
    }

    private void PopulateDisplayUnitList()
    {
#if REVIT2018 || REVIT2019 || REVIT2020
        foreach (DisplayUnitType displayUnitType in UnitUtils.GetValidDisplayUnits(UnitType.UT_Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelFor(displayUnitType)
            });
        }
#else
        foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelForUnit(displayUnitType)
            });
        }
#endif

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == _docDisplayUnits) ?? DisplayUnits.First();
    }

    private void LoadSettings()
    {
        _settings.LoadSettings();

        ReverseDirection = _settings.CreateAlignmentReverseDirection;
        DeleteSourceLines = _settings.CreateAlignmentDeleteSourceLines;

        var displayStation = ((double)_settings.CreateAlignmentStartStation).ToUnit(SelectedDisplayUnit.DisplayUnit);
        StartStation = decimal.Round((decimal)displayStation, 2);

        DrapeOnTopo = _settings.CreateAlignmentDrapeOnTopo;
        SampleInterval = ToDisplay(_settings.CreateAlignmentSampleInterval);
        VerticalTolerance = ToDisplay(_settings.CreateAlignmentVerticalTolerance);
        ElevationOffset = ToDisplay(_settings.CreateAlignmentElevationOffset);
        MinVerticalCurveLength = ToDisplay(_settings.CreateAlignmentMinVerticalCurveLength);

        LoadInputSettings();
    }

    private decimal ToDisplay(decimal internalValue)
    {
        return decimal.Round((decimal)((double)internalValue).ToUnit(SelectedDisplayUnit.DisplayUnit), 3);
    }

    private void SaveSettings(double startStationInternal)
    {
        _settings.LoadSettings();
        _settings.CreateAlignmentReverseDirection = ReverseDirection;
        _settings.CreateAlignmentDeleteSourceLines = DeleteSourceLines;
        _settings.CreateAlignmentStartStation = (decimal)startStationInternal;
        _settings.CreateAlignmentDrapeOnTopo = DrapeOnTopo;
        _settings.CreateAlignmentSampleInterval = (decimal)ToInternal(SampleInterval);
        _settings.CreateAlignmentVerticalTolerance = (decimal)ToInternal(VerticalTolerance);
        _settings.CreateAlignmentElevationOffset = (decimal)ToInternal(ElevationOffset);
        _settings.CreateAlignmentMinVerticalCurveLength = (decimal)ToInternal(MinVerticalCurveLength);
        SaveInputSettings();
        _settings.SaveSettings();
    }

    private void SetDefaultName()
    {
        var existing = AlignmentApi.GetAlignmentElements(App.RevitDocument)
            .Select(AlignmentApi.GetInfo)
            .OfType<AlignmentInfo>()
            .Select(info => info.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var index = 1;
        string candidate;
        do
        {
            candidate = $"Alignment {index}";
            index++;
        }
        while (existing.Contains(candidate));

        AlignmentName = candidate;
    }

    private void UpdateChainStatus()
    {
        if (!IsChainContinuous || _orderedCurves.Count == 0)
        {
            return;
        }

        var length = _orderedCurves.Sum(c => c.Length);
        var displayLength = UnitUtils.ConvertFromInternalUnits(length, SelectedDisplayUnit.DisplayUnit);
        ChainStatus = $"{SelectedCurveCount} lines, continuous, {displayLength:N2} {SelectedDisplayUnit.Label}";
    }
}
