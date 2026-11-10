#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Sweep;
using TopoAlign.Sweep.Road;

namespace TopoAlign.ViewModels.RoadTools;

internal partial class SweepToToposolidViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<SweepToToposolidViewModel>();

    [ObservableProperty]
    private string _roadStatus = "No road selected.";

    [ObservableProperty]
    private List<ToposolidType> _toposolidTypes = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private ToposolidType? _selectedType;

    [ObservableProperty]
    private List<Level> _levels = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private Level? _selectedLevel;

    [ObservableProperty]
    private bool _deleteRoad;

    [ObservableProperty]
    private string _statusNote = string.Empty;

    public string WindowTitle { get; }

    /// <summary>True when the dialog was hidden to pick a road; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public SweepToToposolidRequest? Request { get; private set; }

    private readonly Services.Settings _settings = new();
    private Element? _road;

    public SweepToToposolidViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Sweep to Toposolid {informationVersion}";

        ToposolidTypes = ToposolidBuilder.GetTypes(App.RevitDocument);
        Levels = ToposolidBuilder.GetLevels(App.RevitDocument);

        if (ToposolidTypes.Count == 0)
        {
            StatusNote = "This project has no toposolid types.";
        }
        else if (Levels.Count == 0)
        {
            StatusNote = "This project has no levels.";
        }

        LoadSettings();
    }

    public bool HasRoad => _road is not null;

    private bool CanCreate() => HasRoad && SelectedType is not null && SelectedLevel is not null;

    // ---- choosing the road --------------------------------------------------------------------------------------

    [RelayCommand]
    private void PickRoad()
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PickRequested = true;
        OnHideRequested();
    }

    /// <summary>Lets the user click a road. Called by the command while the dialog is hidden.</summary>
    internal void PickRoadElement()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new RoadPickFilter(), "Select the road to convert to a toposolid");
            SelectRoad(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting a road");
            TaskDialog.Show("Sweep to Toposolid", ex.Message);
        }
    }

    /// <summary>Chooses the road to convert. False, with the reason in the status, if the element isn't a road made by Sweep Along Alignment.</summary>
    internal bool SelectRoad(Element element)
    {
        _road = null;
        try
        {
            if (!RoadBuilder.IsRoad(element))
            {
                RoadStatus = "That element is not a road made by Sweep Along Alignment.";
                return false;
            }

            _road = element;
            var comments = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
            RoadStatus = string.IsNullOrWhiteSpace(comments) ? element.Name : comments!;

            // start on the level the road sits just above
            SelectedLevel = ToposolidBuilder.DefaultLevel(Levels, element) ?? SelectedLevel;
            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasRoad));
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (_road is null || SelectedType is null || SelectedLevel is null)
        {
            return;
        }

        try
        {
            SaveSettings();

            // The toposolid is made by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new SweepToToposolidRequest(_road, SelectedType.Id, SelectedLevel.Id, DeleteRoad);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Sweep to toposolid failed");
            TaskDialog.Show("Sweep to Toposolid", ex.Message);
        }
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        DeleteRoad = _settings.RoadToposolidDeleteRoad;
        SelectedType = ToposolidTypes.FirstOrDefault(t => t.Name == _settings.RoadToposolidType) ?? ToposolidTypes.FirstOrDefault();
        SelectedLevel = Levels.FirstOrDefault();
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.RoadToposolidType = SelectedType?.Name ?? string.Empty;
        _settings.RoadToposolidDeleteRoad = DeleteRoad;
        _settings.SaveSettings();
    }
}
#endif