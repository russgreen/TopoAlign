using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels;

internal sealed class CreateAlignmentRequest
{
    public CreateAlignmentRequest(string name, string json, double planLength, IList<ElementId> sourceCurveIdsToDelete)
    {
        Name = name;
        Json = json;
        PlanLength = planLength;
        SourceCurveIdsToDelete = sourceCurveIdsToDelete;
    }

    public string Name { get; }
    public string Json { get; }

    /// <summary>Alignment length in feet (internal units).</summary>
    public double PlanLength { get; }
    public IList<ElementId> SourceCurveIdsToDelete { get; }
}

internal partial class CreateAlignmentViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<CreateAlignmentViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAlignmentCommand))]
    private string _alignmentName;

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
    private Models.DisplayUnitModel _selectedDisplayUnit;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler HideRequested;

    /// <summary>True when the dialog was hidden to pick lines; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public CreateAlignmentRequest Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly List<ElementId> _selectedCurveIds = new();
    private List<Curve> _orderedCurves = new();

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

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel value)
    {
        UpdateChainStatus();
    }

    private bool CanCreateAlignment()
    {
        return IsChainContinuous &&
               _orderedCurves.Count > 0 &&
               !string.IsNullOrWhiteSpace(AlignmentName);
    }

    [RelayCommand]
    private void PickLines()
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PickRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
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

    [RelayCommand(CanExecute = nameof(CanCreateAlignment))]
    private void CreateAlignment()
    {
        try
        {
            if (_orderedCurves.Count == 0)
            {
                TaskDialog.Show("Create Alignment", "Pick lines first.");
                return;
            }

            if (!AlignmentApi.IsAvailable)
            {
                TaskDialog.Show("Create Alignment", "The Revit Civil Alignments add-in was not found, so alignments cannot be created in this version of Revit.");
                return;
            }

            var curves = ReverseDirection
                ? CurveChainer.ReverseChain(_orderedCurves)
                : _orderedCurves.ToList();

            var startStationInternal = UnitUtils.ConvertToInternalUnits((double)StartStation, SelectedDisplayUnit.DisplayUnit);

            // Alignment JSON is written in shared coordinates; the profile elevation takes the same offset.
            var internalToShared = App.RevitDocument.ActiveProjectLocation.GetTotalTransform().Inverse;
            var json = AlignmentJsonBuilder.Build(AlignmentName, curves, internalToShared, startStationInternal, out var planLength);

            // The alignment is created by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new CreateAlignmentRequest(
                AlignmentName,
                json,
                planLength,
                DeleteSourceLines ? _selectedCurveIds.ToList() : null);

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

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == _docDisplayUnits) ?? DisplayUnits.FirstOrDefault();
    }

    private void LoadSettings()
    {
        _settings.LoadSettings();

        ReverseDirection = _settings.CreateAlignmentReverseDirection;
        DeleteSourceLines = _settings.CreateAlignmentDeleteSourceLines;

        var displayStation = UnitUtils.ConvertFromInternalUnits((double)_settings.CreateAlignmentStartStation, SelectedDisplayUnit.DisplayUnit);
        StartStation = decimal.Round((decimal)displayStation, 2);
    }

    private void SaveSettings(double startStationInternal)
    {
        _settings.LoadSettings();
        _settings.CreateAlignmentReverseDirection = ReverseDirection;
        _settings.CreateAlignmentDeleteSourceLines = DeleteSourceLines;
        _settings.CreateAlignmentStartStation = (decimal)startStationInternal;
        _settings.SaveSettings();
    }

    private void SetDefaultName()
    {
        var existing = AlignmentApi.GetAlignmentElements(App.RevitDocument)
            .Select(AlignmentApi.GetInfo)
            .Where(info => info is not null)
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
