using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels;

/// <summary>What the Edit Alignment dialog hands back to the command: the alignment and its edited JSON.</summary>
internal sealed class EditAlignmentRequest
{
    public EditAlignmentRequest(Element alignment, string name, string json, double planLength, bool renamed, bool startStationChanged, bool reversed)
    {
        Alignment = alignment;
        Name = name;
        Json = json;
        PlanLength = planLength;
        Renamed = renamed;
        StartStationChanged = startStationChanged;
        Reversed = reversed;
    }

    public Element Alignment { get; }
    public string Name { get; }
    public string Json { get; }

    /// <summary>Alignment length in feet (internal units).</summary>
    public double PlanLength { get; }

    public bool Renamed { get; }
    public bool StartStationChanged { get; }
    public bool Reversed { get; }
}

internal partial class EditAlignmentViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<EditAlignmentViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string _alignmentName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private decimal _startStation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private bool _reverseDirection;

    [ObservableProperty]
    private bool _canReverse;

    [ObservableProperty]
    private string _alignmentStatus = "No alignment selected.";

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
    public EditAlignmentRequest? Request { get; private set; }

    private Element? _alignment;
    private string? _originalJson;
    private string _originalName = string.Empty;
    private double _originalStartStationFeet;
    private decimal _originalStartStation;
    private double _planLengthFeet;

    public EditAlignmentViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Edit Alignment {informationVersion}";

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
    }

    /// <summary>True once an alignment has been chosen.</summary>
    public bool HasAlignment => _alignment is not null;

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        // keep the same physical start station when the user switches units
        var feet = UnitUtils.ConvertToInternalUnits((double)StartStation, oldValue.DisplayUnit);
        StartStation = Round(UnitUtils.ConvertFromInternalUnits(feet, newValue.DisplayUnit));
        _originalStartStation = Round(UnitUtils.ConvertFromInternalUnits(_originalStartStationFeet, newValue.DisplayUnit));
        RefreshStatus();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private static decimal Round(double value) => decimal.Round((decimal)value, 3);

    private double ToInternal(decimal displayValue)
    {
        return UnitUtils.ConvertToInternalUnits((double)displayValue, SelectedDisplayUnit.DisplayUnit);
    }

    private bool NameChanged => !string.Equals(AlignmentName?.Trim(), _originalName, StringComparison.Ordinal);

    private bool StartStationChanged => Math.Abs(StartStation - _originalStartStation) > 0.0005m;

    private bool CanApply()
    {
        return HasAlignment &&
               !string.IsNullOrWhiteSpace(AlignmentName) &&
               (NameChanged || StartStationChanged || (ReverseDirection && CanReverse));
    }

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
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), "Select the alignment to edit");
            SelectAlignment(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting an alignment");
            TaskDialog.Show("Edit Alignment", ex.Message);
        }
    }

    /// <summary>Reads an alignment's name, start station and length into the dialog. False, with the reason in the status, if it can't be edited.</summary>
    internal bool SelectAlignment(Element element)
    {
        _alignment = null;
        _originalJson = null;
        ReverseDirection = false;
        CanReverse = false;
        OnPropertyChanged(nameof(HasAlignment));

        try
        {
            if (!AlignmentApi.IsValid(element))
            {
                AlignmentStatus = "That element is not an alignment.";
                return false;
            }

            var json = AlignmentApi.GetJson(element);
            var document = AlignmentJsonDocument.Parse(json ?? throw new InvalidOperationException("The alignment has no JSON data."));

            _alignment = element;
            _originalJson = json;
            _originalName = document.Name;
            _originalStartStationFeet = document.StartStation;
            _planLengthFeet = document.PlanLength;
            _originalStartStation = Round(UnitUtils.ConvertFromInternalUnits(document.StartStation, SelectedDisplayUnit.DisplayUnit));

            AlignmentName = document.Name;
            StartStation = _originalStartStation;
            CanReverse = document.CanReverse(out var reason);
            RefreshStatus(CanReverse ? null : reason);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _alignment = null;
            AlignmentStatus = ex.Message;
            return false;
        }
        finally
        {
            OnPropertyChanged(nameof(HasAlignment));
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    private void RefreshStatus(string? note = null)
    {
        if (_alignment is null || SelectedDisplayUnit is null)
        {
            return;
        }

        var length = UnitUtils.ConvertFromInternalUnits(_planLengthFeet, SelectedDisplayUnit.DisplayUnit);
        var text = $"{(string.IsNullOrEmpty(_originalName) ? "Alignment" : _originalName)}, length {length:N2} {SelectedDisplayUnit.Label}";
        AlignmentStatus = note is null ? text : $"{text}. {note}";
    }

    // ---- applying -----------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (_alignment is null || _originalJson is null)
        {
            return;
        }

        try
        {
            var document = AlignmentJsonDocument.Parse(_originalJson);
            var name = AlignmentName.Trim();
            var renamed = NameChanged;
            var startChanged = StartStationChanged;
            var reversed = ReverseDirection && CanReverse;

            if (renamed)
            {
                document.Name = name;
            }

            if (startChanged)
            {
                document.StartStation = ToInternal(StartStation);
            }

            if (reversed)
            {
                document.Reverse();
            }

            // The alignment is updated by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new EditAlignmentRequest(_alignment, name, document.ToJson(), document.PlanLength, renamed, startChanged, reversed);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Edit alignment failed");
            TaskDialog.Show("Edit Alignment", ex.Message);
        }
    }
}
