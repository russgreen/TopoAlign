using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>What the Sweep Along Alignment dialog hands back to the command.</summary>
internal sealed class SweepAlignmentRequest
{
    public SweepAlignmentRequest(Element alignment, string alignmentName, SectionProfile profile, RoadOptions options)
    {
        Alignment = alignment;
        AlignmentName = alignmentName;
        Profile = profile;
        Options = options;
    }

    public Element Alignment { get; }
    public string AlignmentName { get; }

    /// <summary>The cross-section, already mirrored if the user asked for that.</summary>
    public SectionProfile Profile { get; }

    public RoadOptions Options { get; }
}

internal partial class SweepAlignmentViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<SweepAlignmentViewModel>();

    [ObservableProperty]
    private string _alignmentStatus = "No alignment selected.";

    [ObservableProperty]
    private List<ProfileTypeInfo> _profileTypes = new();

    [ObservableProperty]
    private ProfileTypeInfo _selectedProfileType;

    [ObservableProperty]
    private string _profileStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _interval;

    [ObservableProperty]
    private decimal _verticalOffset;

    [ObservableProperty]
    private bool _mirrorProfile;

    [ObservableProperty]
    private List<RoadCategory> _categories = new();

    [ObservableProperty]
    private RoadCategory _selectedCategory;

    [ObservableProperty]
    private string _existingRoadNote = string.Empty;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler HideRequested;

    /// <summary>True when the dialog was hidden to pick an alignment; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public SweepAlignmentRequest Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly Dictionary<long, (SectionProfile Profile, string Error)> _profileCache = new();
    private Element _alignment;
    private string _alignmentName = string.Empty;
    private SectionProfile _profile;

    public SweepAlignmentViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Sweep Along Alignment {informationVersion}";

        var documentUnits = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
        foreach (ForgeTypeId unit in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = unit,
                Label = LabelUtils.GetLabelForUnit(unit)
            });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == documentUnits) ?? DisplayUnits.FirstOrDefault();

        Categories = RoadBuilder.GetCategories(App.RevitDocument);
        ProfileTypes = SectionProfileReader.ListProfileTypes(App.RevitDocument);
        ProfileStatus = ProfileTypes.Count == 0
            ? "No profile families are loaded. Load one from Revit's Profiles library, or draw your own closed shape in a profile family."
            : string.Empty;

        LoadSettings();
    }

    public bool HasAlignment => _alignment is not null;

    private bool CanCreate() => HasAlignment && _profile is not null && Interval > 0m;

    private double ToInternal(decimal displayValue)
    {
        return UnitUtils.ConvertToInternalUnits((double)displayValue, SelectedDisplayUnit.DisplayUnit);
    }

    private decimal ToDisplay(decimal internalValue)
    {
        return decimal.Round((decimal)UnitUtils.ConvertFromInternalUnits((double)internalValue, SelectedDisplayUnit.DisplayUnit), 3);
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
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), "Select the alignment to build the road along");
            SelectAlignment(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting an alignment");
            TaskDialog.Show("Sweep Along Alignment", ex.Message);
        }
    }

    /// <summary>Chooses the alignment the road follows. False, with the reason in the status, if the element isn't an alignment.</summary>
    internal bool SelectAlignment(Element element)
    {
        _alignment = null;
        ExistingRoadNote = string.Empty;
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
            var length = UnitUtils.ConvertFromInternalUnits(info.DisplayedEndStation - info.DisplayedStartStation, SelectedDisplayUnit.DisplayUnit);
            AlignmentStatus = $"{_alignmentName}, length {length:N2} {SelectedDisplayUnit.Label}";

            if (RoadBuilder.FindRoads(App.RevitDocument, element).Count > 0)
            {
                ExistingRoadNote = "A road already exists for this alignment. Creating again replaces it.";
            }

            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasAlignment));
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- the cross-section --------------------------------------------------------------------------------------

    partial void OnSelectedProfileTypeChanged(ProfileTypeInfo value) => LoadProfile(value);

    partial void OnMirrorProfileChanged(bool value) => RefreshProfileStatus();

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel value) => RefreshProfileStatus();

    /// <summary>Reads the shape drawn in the chosen profile family (once per type) and shows its size.</summary>
    private void LoadProfile(ProfileTypeInfo type)
    {
        _profile = null;
        if (type is null)
        {
            CreateCommand.NotifyCanExecuteChanged();
            return;
        }

        var key = ElementIdValue(type.SymbolId);
        if (!_profileCache.TryGetValue(key, out var cached))
        {
            var profile = SectionProfileReader.Read(App.RevitDocument, type.SymbolId, out var error);
            cached = (profile, error);
            _profileCache[key] = cached;
        }

        _profile = cached.Profile;
        if (_profile is null)
        {
            ProfileStatus = cached.Error;
        }
        else
        {
            RefreshProfileStatus();
        }

        CreateCommand.NotifyCanExecuteChanged();
    }

    private void RefreshProfileStatus()
    {
        if (_profile is null || SelectedDisplayUnit is null)
        {
            return;
        }

        var unit = SelectedDisplayUnit.DisplayUnit;
        var width = UnitUtils.ConvertFromInternalUnits(_profile.Width, unit);
        var height = UnitUtils.ConvertFromInternalUnits(_profile.Height, unit);
        ProfileStatus = $"Section {width:N2} wide by {height:N2} high ({SelectedDisplayUnit.Label})" +
                        (MirrorProfile ? ", flipped left to right." : ".");
    }

    private static long ElementIdValue(ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        try
        {
            var options = new RoadOptions
            {
                Interval = ToInternal(Interval),
                VerticalOffset = ToInternal(VerticalOffset),
                Category = SelectedCategory?.Category ?? BuiltInCategory.OST_GenericModel,
                ProfileName = SelectedProfileType?.Name ?? string.Empty
            };

            SaveSettings();

            // The road is built by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new SweepAlignmentRequest(_alignment, _alignmentName, MirrorProfile ? _profile.Mirrored() : _profile, options);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Sweep along alignment failed");
            TaskDialog.Show("Sweep Along Alignment", ex.Message);
        }
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        Interval = ToDisplay(_settings.SweepInterval);
        VerticalOffset = ToDisplay(_settings.SweepVerticalOffset);
        MirrorProfile = _settings.SweepMirrorProfile;

        SelectedCategory = Categories.FirstOrDefault(c => c.Category.ToString() == _settings.SweepCategory) ?? Categories.FirstOrDefault();
        SelectedProfileType = ProfileTypes.FirstOrDefault(t => t.Name == _settings.SweepProfileName) ?? ProfileTypes.FirstOrDefault();
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.SweepInterval = (decimal)ToInternal(Interval);
        _settings.SweepVerticalOffset = (decimal)ToInternal(VerticalOffset);
        _settings.SweepMirrorProfile = MirrorProfile;
        _settings.SweepCategory = SelectedCategory?.Category.ToString() ?? string.Empty;
        _settings.SweepProfileName = SelectedProfileType?.Name ?? string.Empty;
        _settings.SaveSettings();
    }
}
