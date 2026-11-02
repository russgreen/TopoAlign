using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Globalization;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels;

/// <summary>An entry in the label type list: an existing type, or "create Autodesk's recommended type".</summary>
internal sealed class StationLabelTypeItem
{
    public StationLabelTypeItem(ElementId id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary><see cref="ElementId.InvalidElementId"/> means the recommended type is created when the labels are.</summary>
    public ElementId Id { get; }
    public string Name { get; }
}

/// <summary>What the Station Labels dialog hands back to the command.</summary>
internal sealed class StationLabelsRequest
{
    public StationLabelsRequest(Element alignment, string alignmentName, View view, ElementId labelTypeId,
        double? startStation, double? endStation, double? interval, double? offset, bool hasLeader)
    {
        Alignment = alignment;
        AlignmentName = alignmentName;
        View = view;
        LabelTypeId = labelTypeId;
        StartStation = startStation;
        EndStation = endStation;
        Interval = interval;
        Offset = offset;
        HasLeader = hasLeader;
    }

    public Element Alignment { get; }
    public string AlignmentName { get; }
    public View View { get; }
    public ElementId LabelTypeId { get; }

    /// <summary>Feet (internal units); null means the alignment's own range.</summary>
    public double? StartStation { get; }
    public double? EndStation { get; }

    /// <summary>Feet (internal units); null means Autodesk's default for the document's stationing units.</summary>
    public double? Interval { get; }

    /// <summary>Paper-space feet, positive to the right of the alignment; null means Autodesk's default.</summary>
    public double? Offset { get; }

    public bool HasLeader { get; }
}

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
            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasAlignment));
            CreateCommand.NotifyCanExecuteChanged();
        }
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
