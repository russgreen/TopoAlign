using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nice3point.Revit.Extensions;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.Alignments.Profile;
using TopoAlign.Preview;

namespace TopoAlign.ViewModels.AlignmentTools;

internal enum TJunctionPick
{
    None,
    Main,
    Side,
    MainRoad
}

/// <summary>One alignment to create: its name, JSON and plan length.</summary>
internal sealed class TJunctionNewAlignment
{
    public TJunctionNewAlignment(string name, string json, double planLength)
    {
        Name = name;
        Json = json;
        PlanLength = planLength;
    }

    public string Name { get; }
    public string Json { get; }
    public double PlanLength { get; }
}

/// <summary>What the T-Junction dialog hands back to the command: the trimmed side road and the two kerb returns.</summary>
internal sealed class TJunctionRequest
{
    public TJunctionRequest(Element side, string sideName, string sideJson, double sidePlanLength, IReadOnlyList<TJunctionNewAlignment> kerbReturns, IReadOnlyList<XYZ>? surfaceOutline, double surfaceThickness)
    {
        Side = side;
        SideName = sideName;
        SideJson = sideJson;
        SidePlanLength = sidePlanLength;
        KerbReturns = kerbReturns;
        SurfaceOutline = surfaceOutline;
        SurfaceThickness = surfaceThickness;
    }

    public Element Side { get; }
    public string SideName { get; }
    public string SideJson { get; }
    public double SidePlanLength { get; }
    public IReadOnlyList<TJunctionNewAlignment> KerbReturns { get; }

    /// <summary>Outline of the road surface at the junction in internal coordinates, counter-clockwise from above; null for no surface.</summary>
    public IReadOnlyList<XYZ>? SurfaceOutline { get; }

    public double SurfaceThickness { get; }
}

internal partial class TJunctionViewModel : BaseViewModel, IDisposable
{
    private static readonly ILogger log = Log.ForContext<TJunctionViewModel>();

    private readonly Services.Settings _settings = new();

    [ObservableProperty]
    private string _mainStatus = "No main alignment selected.";

    [ObservableProperty]
    private string _sideStatus = "No side alignment selected.";

    [ObservableProperty]
    private string _roadStatus = "No road element picked: kerb levels follow the main alignment profile.";

    [ObservableProperty]
    private string _levelReadout = string.Empty;

    /// <summary>Added to the main road level at the kerb returns, on top of the alignment or road element level.</summary>
    [ObservableProperty]
    private decimal _mainLevelOffset;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _kerbRadius;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _sideHalfWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _mainHalfWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _useOtherEnd;

    [ObservableProperty]
    private bool _createSurface = true;

    [ObservableProperty]
    private decimal _surfaceThickness;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    public string WindowTitle { get; }

    /// <summary>Which alignment the dialog was hidden to pick; the command picks, then shows the dialog again.</summary>
    public TJunctionPick PendingPick { get; private set; }

    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public TJunctionRequest? Request { get; private set; }

    private Element? _main;
    private Element? _side;
    private Element? _mainRoad;
    private RoadElementLevel? _roadLevel;
    private AlignmentJsonDocument? _mainDocument;
    private AlignmentJsonDocument? _sideDocument;
    private List<PlanSegment>? _mainPlan;
    private List<PlanSegment>? _sidePlan;
    private TJunctionResult? _result;
    private AlignmentPickPreview? _preview;
    private bool _previewTried;

    public TJunctionViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"T-Junction {informationVersion}";

        var documentUnits = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
        foreach (ForgeTypeId unit in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = unit,
                Label = LabelUtils.GetLabelForUnit(unit)
            });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == documentUnits) ?? DisplayUnits.First();

        LoadSettings();
    }

    public bool HasMain => _main is not null;

    public bool HasSide => _side is not null;

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        KerbRadius = Helpers.UnitConversion.Convert(KerbRadius, oldValue.DisplayUnit, newValue.DisplayUnit);
        SideHalfWidth = Helpers.UnitConversion.Convert(SideHalfWidth, oldValue.DisplayUnit, newValue.DisplayUnit);
        MainHalfWidth = Helpers.UnitConversion.Convert(MainHalfWidth, oldValue.DisplayUnit, newValue.DisplayUnit);
        SurfaceThickness = Helpers.UnitConversion.Convert(SurfaceThickness, oldValue.DisplayUnit, newValue.DisplayUnit);
        MainLevelOffset = Helpers.UnitConversion.Convert(MainLevelOffset, oldValue.DisplayUnit, newValue.DisplayUnit);
        UpdateLevelReadout();
    }

    partial void OnMainLevelOffsetChanged(decimal value) => UpdateLevelReadout();

    partial void OnKerbRadiusChanged(decimal value) => Recompute();

    partial void OnSideHalfWidthChanged(decimal value) => Recompute();

    partial void OnMainHalfWidthChanged(decimal value) => Recompute();

    partial void OnUseOtherEndChanged(bool value) => Recompute();

    private double ToInternal(decimal displayValue) => ((double)displayValue).FromUnit(SelectedDisplayUnit.DisplayUnit);

    private decimal ToDisplay(decimal internalValue) => decimal.Round((decimal)((double)internalValue).ToUnit(SelectedDisplayUnit.DisplayUnit), 3);

    private bool CanCreate() => _result is { IsValid: true };

    // ---- choosing the alignments --------------------------------------------------------------------------------

    [RelayCommand]
    private void PickMain() => RequestPick(TJunctionPick.Main);

    [RelayCommand]
    private void PickSide() => RequestPick(TJunctionPick.Side);

    private void RequestPick(TJunctionPick kind)
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PendingPick = kind;
        PickRequested = true;
        OnHideRequested();
    }

    /// <summary>Runs the pick the dialog asked for. Called by the command while the dialog is hidden.</summary>
    internal void RunPendingPick()
    {
        switch (PendingPick)
        {
            case TJunctionPick.Main:
                AlignmentPicker.PickAndSelect("Select the main road alignment", "T-Junction", element => SelectMain(element), log);
                break;

            case TJunctionPick.Side:
                AlignmentPicker.PickAndSelect("Select the side road alignment", "T-Junction", element => SelectSide(element), log);
                break;

            case TJunctionPick.MainRoad:
                PickMainRoadElement();
                break;
        }

        PendingPick = TJunctionPick.None;
    }

    [RelayCommand]
    private void PickMainRoad() => RequestPick(TJunctionPick.MainRoad);

    [RelayCommand]
    private void ClearMainRoad() => SelectMainRoad(null);

    /// <summary>Lets the user click the element that models the main road surface. Called by the command while the dialog is hidden.</summary>
    private void PickMainRoadElement()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var reference = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Select the element that is the main road surface");
            SelectMainRoad(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting the main road element");
            TaskDialog.Show("T-Junction", ex.Message);
        }
    }

    /// <summary>Chooses the element whose surface gives the main road level at the kerbs; null clears it. False if no ray can be cast at it.</summary>
    internal bool SelectMainRoad(Element? element)
    {
        _mainRoad = null;
        _roadLevel = null;
        try
        {
            if (element is null)
            {
                RoadStatus = "No road element picked: kerb levels follow the main alignment profile.";
                return true;
            }

            _roadLevel = RoadElementLevel.TryCreate(App.RevitDocument, element.Id);
            if (_roadLevel is null)
            {
                RoadStatus = "The document has no 3D view to check the road geometry in.";
                return false;
            }

            _mainRoad = element;
            RoadStatus = string.IsNullOrWhiteSpace(element.Name) ? $"Element {element.Id}" : element.Name;
            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasMainRoad));
            UpdateLevelReadout();
        }
    }

    public bool HasMainRoad => _mainRoad is not null;

    internal bool SelectMain(Element element) => Select(element, isMain: true);

    internal bool SelectSide(Element element) => Select(element, isMain: false);

    private bool Select(Element element, bool isMain)
    {
        void SetStatus(string text)
        {
            if (isMain)
            {
                MainStatus = text;
            }
            else
            {
                SideStatus = text;
            }
        }

        if (isMain)
        {
            _main = null;
            _mainDocument = null;
            _mainPlan = null;
            OnPropertyChanged(nameof(HasMain));
        }
        else
        {
            _side = null;
            _sideDocument = null;
            _sidePlan = null;
            OnPropertyChanged(nameof(HasSide));
        }

        try
        {
            if (!AlignmentApi.IsValid(element))
            {
                SetStatus("That element is not an alignment.");
                return false;
            }

            var other = isMain ? _side : _main;
            if (other is not null && other.Id == element.Id)
            {
                SetStatus("The main and side roads must be different alignments.");
                return false;
            }

            var json = AlignmentApi.GetJson(element) ?? throw new InvalidOperationException("The alignment has no JSON data.");
            var document = AlignmentJsonDocument.Parse(json);
            if (!document.CanEditPlan(out var reason))
            {
                SetStatus(reason ?? "The plan of this alignment can't be read.");
                return false;
            }

            var plan = document.ReadPlan();
            if (isMain)
            {
                _main = element;
                _mainDocument = document;
                _mainPlan = plan;
            }
            else
            {
                _side = element;
                _sideDocument = document;
                _sidePlan = plan;
            }

            var length = UnitUtils.ConvertFromInternalUnits(document.PlanLength, SelectedDisplayUnit.DisplayUnit);
            SetStatus($"{(string.IsNullOrEmpty(document.Name) ? "Alignment" : document.Name)}, length {length:N2} {SelectedDisplayUnit.Label}");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message);
            return false;
        }
        finally
        {
            OnPropertyChanged(nameof(HasMain));
            OnPropertyChanged(nameof(HasSide));
            Recompute();
        }
    }

    // ---- layout and preview -------------------------------------------------------------------------------------

    private void Recompute()
    {
        _result = null;
        ResultMessage = string.Empty;

        if (_mainPlan is not null && _sidePlan is not null && SelectedDisplayUnit is not null)
        {
            try
            {
                _result = TJunctionGeometry.Create(_mainPlan, _sidePlan, ToInternal(KerbRadius), ToInternal(SideHalfWidth),
                    ToInternal(MainHalfWidth), UseOtherEnd);
                ResultMessage = string.Join(Environment.NewLine, _result.Errors);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "T-junction layout failed");
                ResultMessage = ex.Message;
            }
        }

        UpdatePreview();
        UpdateLevelReadout();
        CreateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The main road level at a plan point: road element if picked (and hit), else the alignment profile, plus the manual offset.</summary>
    private Func<PlanPoint, double>? MainLevelFunction()
    {
        if (_mainDocument is null || _mainPlan is null)
        {
            return null;
        }

        var mainProfile = _mainDocument.ReadProfile();
        var mainPlan = _mainPlan;
        double AlignmentLevel(PlanPoint p) => ProfileEditing.ElevationAt(mainProfile, TJunctionGeometry.StationAlong(mainPlan, p));
        Func<PlanPoint, double?>? road = _roadLevel is { } level ? level.LevelAt : null;
        return TJunctionLevels.MainLevel(AlignmentLevel, road, ToInternal(MainLevelOffset));
    }

    /// <summary>Shows how far the kerb levels at the main road sit from the main alignment's profile level.</summary>
    private void UpdateLevelReadout()
    {
        LevelReadout = string.Empty;
        if (_result is not { IsValid: true } result || SelectedDisplayUnit is null || MainLevelFunction() is not { } level)
        {
            return;
        }

        try
        {
            var mainProfile = _mainDocument!.ReadProfile();
            string Offset(PlanPoint p)
            {
                var delta = level(p) - ProfileEditing.ElevationAt(mainProfile, TJunctionGeometry.StationAlong(_mainPlan!, p));
                return $"{UnitUtils.ConvertFromInternalUnits(delta, SelectedDisplayUnit.DisplayUnit):+0.000;-0.000;0.000}";
            }

            LevelReadout = $"Kerb level vs main alignment: left {Offset(result.KerbLeft!.EndPoint)}, right {Offset(result.KerbRight!.EndPoint)} {SelectedDisplayUnit.Label}";
        }
        catch (Exception ex)
        {
            log.Warning(ex, "T-junction level read-out failed");
        }
    }

    private void UpdatePreview()
    {
        try
        {
            if (!_previewTried)
            {
                _previewTried = true;
                _preview = AlignmentPickPreview.TryStart(App.CachedUiApp.ActiveUIDocument);
            }

            if (_preview is null)
            {
                return;
            }

            if (_result is not { IsValid: true } result)
            {
                _preview.SetSegments(Array.Empty<PlanSegment>());
                return;
            }

            var sharedToInternal = App.RevitDocument.ActiveProjectLocation.GetTotalTransform();
            PlanPoint Map(PlanPoint p)
            {
                var q = sharedToInternal.OfPoint(new XYZ(p.X, p.Y, 0.0));
                return new PlanPoint(q.X, q.Y);
            }

            var shown = new List<PlanSegment>();
            shown.AddRange(TJunctionGeometry.Map(result.TrimmedSide, Map));
            shown.AddRange(TJunctionGeometry.Map(new PlanSegment[] { result.KerbLeft!, result.KerbRight! }, Map));
            _preview.SetSegments(shown);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "T-junction preview failed");
        }
    }

    public void Dispose()
    {
        _preview?.Dispose();
        _preview = null;
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (_result is not { IsValid: true } result || _side is null || _sideDocument is null)
        {
            return;
        }

        try
        {
            SaveSettings();

            // work on a fresh copy so the dialog can be reused if anything below fails
            var sideDocument = AlignmentJsonDocument.Parse(_sideDocument.ToJson());
            var sideLength = sideDocument.SetPlan(result.TrimmedSide);

            var sideProfile = sideDocument.ReadProfile();
            var mainElevation = MainLevelFunction()!;

            double SideElevation(PlanPoint p) => ProfileEditing.ElevationAt(sideProfile, TJunctionGeometry.StationAlong(result.TrimmedSide, p));
            double MainElevation(PlanPoint p) => mainElevation(p);

            var sideName = string.IsNullOrWhiteSpace(sideDocument.Name) ? "Side road" : sideDocument.Name;
            var kerbs = new List<TJunctionNewAlignment>();
            foreach (var (suffix, arc) in new[] { ("L", result.KerbLeft!), ("R", result.KerbRight!) })
            {
                var name = $"{sideName} kerb {suffix}";

                // the geometry is already in shared coordinates, so no transform is needed; the profile runs from the
                // side road's elevation at the start of the return to the main road's elevation at its end
                var startElevation = SideElevation(arc.StartPoint);
                var endElevation = MainElevation(arc.EndPoint);
                var profile = new[] { new ProfilePoint(0.0, startElevation), new ProfilePoint(arc.Length, endElevation) };
                var json = AlignmentJsonBuilder.BuildFromSegments(name, new PlanSegment[] { arc }, Transform.Identity, 0.0, startElevation,
                    profile, 0.0, 0.005, out var length);
                kerbs.Add(new TJunctionNewAlignment(name, json, length));
            }

            List<XYZ>? surface = null;
            if (CreateSurface)
            {
                var outline = TJunctionSurface.Outline(result, SideElevation, MainElevation);
                if (outline is null || TJunctionSurface.Triangulate(outline.Select(p => p.Point).ToList()) is null)
                {
                    TaskDialog.Show("T-Junction", "The road surface at the junction could not be formed. Untick the surface option or change the radius.");
                    return;
                }

                // outline elevations are shared; the transform takes them to internal along with the plan position
                var sharedToInternal = App.RevitDocument.ActiveProjectLocation.GetTotalTransform();
                surface = outline.Select(p => sharedToInternal.OfPoint(new XYZ(p.Point.X, p.Point.Y, p.Elevation))).ToList();
            }

            // The alignments are updated and created by the command once this dialog has closed, so the transactions
            // run inside the Revit API context rather than from a WPF button handler.
            Request = new TJunctionRequest(_side, sideName, sideDocument.ToJson(), sideLength, kerbs, surface, ToInternal(SurfaceThickness));
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "T-junction failed");
            TaskDialog.Show("T-Junction", ex.Message);
        }
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();
        KerbRadius = ToDisplay(_settings.TJunctionKerbRadius);
        SideHalfWidth = ToDisplay(_settings.TJunctionSideHalfWidth);
        MainHalfWidth = ToDisplay(_settings.TJunctionMainHalfWidth);
        SurfaceThickness = ToDisplay(_settings.TJunctionSurfaceThickness);
        CreateSurface = _settings.TJunctionCreateSurface;
        MainLevelOffset = ToDisplay(_settings.TJunctionMainLevelOffset);
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.TJunctionKerbRadius = (decimal)ToInternal(KerbRadius);
        _settings.TJunctionSideHalfWidth = (decimal)ToInternal(SideHalfWidth);
        _settings.TJunctionMainHalfWidth = (decimal)ToInternal(MainHalfWidth);
        _settings.TJunctionSurfaceThickness = (decimal)ToInternal(SurfaceThickness);
        _settings.TJunctionCreateSurface = CreateSurface;
        _settings.TJunctionMainLevelOffset = (decimal)ToInternal(MainLevelOffset);
        _settings.SaveSettings();
    }
}
