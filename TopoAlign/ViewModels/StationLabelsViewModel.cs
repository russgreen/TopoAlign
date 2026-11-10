using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Globalization;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Civil;

namespace TopoAlign.ViewModels;

internal partial class StationLabelsViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<StationLabelsViewModel>();

    [ObservableProperty]
    private string _alignmentStatus = "No alignment selected.";

    [ObservableProperty]
    private string _viewStatus = string.Empty;

    [ObservableProperty]
    private string _inputMessage = string.Empty;

    [ObservableProperty]
    private List<StationLabelTypeItem> _labelTypes = new();

    [ObservableProperty]
    private StationLabelTypeItem? _selectedLabelType;

    [ObservableProperty]
    private string _intervalText = string.Empty;

    [ObservableProperty]
    private string _offsetText = string.Empty;

    [ObservableProperty]
    private string _startStationText = string.Empty;

    [ObservableProperty]
    private string _endStationText = string.Empty;

    [ObservableProperty]
    private bool _hasLeader = true;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>True when the dialog was hidden to pick an alignment; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public StationLabelsRequest? Request { get; private set; }

    private readonly View _view;
    private Element? _alignment;
    private string _alignmentName = string.Empty;
    private string _suggestedInterval = string.Empty;

    public StationLabelsViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Station Labels {informationVersion}";

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

        _view = App.RevitDocument.ActiveView;
        ViewStatus = DescribeView(_view);
        LoadLabelTypes();
    }

    public bool HasAlignment => _alignment is not null;

    /// <summary>Spot dimensions, which station labels are made of, can't go in every kind of view.</summary>
    public bool IsViewSupported => IsSupported(_view);

    internal static bool IsSupported(View view)
    {
        if (view is null)
        {
            return false;
        }

        switch (view.ViewType)
        {
            case ViewType.FloorPlan:
            case ViewType.EngineeringPlan:
            case ViewType.CeilingPlan:
            case ViewType.AreaPlan:
            case ViewType.Section:
            case ViewType.Elevation:
            case ViewType.Detail:
                return true;
            default:
                return false;
        }
    }

    private static string DescribeView(View view)
    {
        if (view is null)
        {
            return "There is no active view.";
        }

        return IsSupported(view)
            ? $"Labels will be placed in the active view, '{view.Name}'."
            : $"The active view, '{view.Name}' ({view.ViewType}), can't hold station labels. Open a plan, section or elevation view first.";
    }

    private bool CanCreate() => HasAlignment && IsViewSupported && SelectedLabelType is not null;

    private void LoadLabelTypes()
    {
        var types = AlignmentApi.GetStationLabelTypes(App.RevitDocument);
        var items = new List<StationLabelTypeItem>();
        foreach (var type in types)
        {
            items.Add(new StationLabelTypeItem(type.Id, type.IsRecommended ? $"{type.Name} (recommended)" : type.Name));
        }

        if (!types.Any(t => t.IsRecommended))
        {
            // nothing suitable in the model yet: Autodesk creates its recommended type with the labels
            items.Insert(0, new StationLabelTypeItem(ElementId.InvalidElementId, "New recommended type"));
        }

        LabelTypes = items;
        SelectedLabelType = items.FirstOrDefault();
    }

    partial void OnSelectedLabelTypeChanged(StationLabelTypeItem? value) => CreateCommand.NotifyCanExecuteChanged();

    // ---- choosing the alignment ---------------------------------------------------------------------------------

    [RelayCommand]
    private void PickAlignment()
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PickRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Lets the user click an alignment. Called by the command while the dialog is hidden.</summary>
    internal void PickAlignmentElement()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), "Select the alignment to label");
            SelectAlignment(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting an alignment");
            TaskDialog.Show("Station Labels", ex.Message);
        }
    }

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        var wasSuggested = IntervalText == _suggestedInterval;
        IntervalText = Helpers.UnitConversion.ConvertText(IntervalText, oldValue.DisplayUnit, newValue.DisplayUnit);
        OffsetText = Helpers.UnitConversion.ConvertText(OffsetText, oldValue.DisplayUnit, newValue.DisplayUnit);
        StartStationText = Helpers.UnitConversion.ConvertText(StartStationText, oldValue.DisplayUnit, newValue.DisplayUnit);
        EndStationText = Helpers.UnitConversion.ConvertText(EndStationText, oldValue.DisplayUnit, newValue.DisplayUnit);
        if (wasSuggested)
        {
            _suggestedInterval = IntervalText;
        }

        if (_alignment is not null)
        {
            SelectAlignment(_alignment);
        }
    }

    /// <summary>Chooses the alignment to label. False, with the reason in the status, if the element isn't an alignment.</summary>
    internal bool SelectAlignment(Element element)
    {
        _alignment = null;
        try
        {
            var info = AlignmentApi.GetInfo(element);
            if (info is null)
            {
                AlignmentStatus = "That element is not an alignment.";
                return false;
            }

            _alignment = element;
            _alignmentName = string.IsNullOrWhiteSpace(info.Name) ? "Alignment" : info.Name;
            var unit = SelectedDisplayUnit.DisplayUnit;
            var from = UnitUtils.ConvertFromInternalUnits(info.DisplayedStartStation, unit);
            var to = UnitUtils.ConvertFromInternalUnits(info.DisplayedEndStation, unit);
            AlignmentStatus = $"{_alignmentName}, stations {from:N2} to {to:N2} {SelectedDisplayUnit.Label}";

            // Autodesk's default interval (100 ft / 1000 m) is often longer than a site alignment, which would
            // leave a single label at the start, so suggest one that gives around ten labels.
            if (string.IsNullOrWhiteSpace(IntervalText) || IntervalText == _suggestedInterval)
            {
                _suggestedInterval = SuggestInterval(to - from);
                IntervalText = _suggestedInterval;
            }

            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasAlignment));
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>A round interval (1, 2 or 5 times a power of ten) that gives roughly ten labels over the length, in the display units.</summary>
    internal static string SuggestInterval(double length)
    {
        if (double.IsNaN(length) || length <= 0)
        {
            return string.Empty;
        }

        var raw = length / 10;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var factor = raw / magnitude;
        var nice = (factor >= 5 ? 5 : factor >= 2 ? 2 : 1) * magnitude;
        return nice.ToString("0.######", CultureInfo.CurrentCulture);
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    /// <summary>Parses a length typed in the dialog. Blank gives null (use the default); anything else must be a number.</summary>
    private bool TryParse(string text, string what, bool mustBePositive, out double? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            InputMessage = $"The {what} '{text.Trim()}' is not a number.";
            return false;
        }

        if (mustBePositive && value <= 0m)
        {
            InputMessage = $"The {what} must be greater than zero.";
            return false;
        }

        result = UnitUtils.ConvertToInternalUnits((double)value, SelectedDisplayUnit.DisplayUnit);
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        InputMessage = string.Empty;

        if (_alignment is null || SelectedLabelType is null)
        {
            return;
        }

        if (!TryParse(IntervalText, "interval", true, out var interval) ||
            !TryParse(OffsetText, "offset", false, out var offset) ||
            !TryParse(StartStationText, "start station", false, out var start) ||
            !TryParse(EndStationText, "end station", false, out var end))
        {
            return;
        }

        if (start is not null && end is not null && start > end)
        {
            InputMessage = "The start station must not be after the end station.";
            return;
        }

        // The labels are created by the command once this dialog has closed, so the transaction
        // runs inside the Revit API context rather than from a WPF button handler.
        Request = new StationLabelsRequest(_alignment, _alignmentName, _view, SelectedLabelType.Id, start, end, interval, offset, HasLeader);
        OnClosingRequest();
    }
}