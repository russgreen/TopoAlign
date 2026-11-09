using Nice3point.Revit.Extensions;
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

internal partial class SweepAlignmentViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<SweepAlignmentViewModel>();

    public List<RoadChoice<RoadGeometryMethod>> GeometryMethods { get; } = new()
    {
        new RoadChoice<RoadGeometryMethod>(RoadGeometryMethod.SegmentedLoft, "Segmented loft"),
        new RoadChoice<RoadGeometryMethod>(RoadGeometryMethod.SmoothSurfaces, "Smooth surfaces")
    };

    public List<RoadChoice<RoadOutput>> Outputs { get; } = new()
    {
        new RoadChoice<RoadOutput>(RoadOutput.DirectShape, "Direct shape"),
        new RoadChoice<RoadOutput>(RoadOutput.Family, "Family"),
#if REVIT2024_OR_GREATER
        new RoadChoice<RoadOutput>(RoadOutput.Toposolid, "Toposolid only (no road)")
#endif
    };

    [ObservableProperty]
    private RoadChoice<RoadGeometryMethod>? _selectedGeometryMethod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToposolidOutput))]
    [NotifyPropertyChangedFor(nameof(IsRoadOutput))]
    [NotifyPropertyChangedFor(nameof(CreateButtonText))]
    [NotifyPropertyChangedFor(nameof(ExistingRoadNote))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private RoadChoice<RoadOutput>? _selectedOutput;

    /// <summary>Toposolid types in the project, offered when the road is only used to make a toposolid.</summary>
    public List<RoadChoice<ElementId>> ToposolidTypes { get; } = new();

    /// <summary>Levels in the project, lowest first.</summary>
    public List<RoadChoice<ElementId>> Levels { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private RoadChoice<ElementId>? _selectedToposolidType;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private RoadChoice<ElementId>? _selectedLevel;

    /// <summary>True when no road is added and the swept geometry only makes a toposolid.</summary>
    public bool IsToposolidOutput => !IsAssemblyMode && SelectedOutput?.Value == RoadOutput.Toposolid;

    /// <summary>True when a road is added to the model, so it has a category.</summary>
    public bool IsRoadOutput => !IsToposolidOutput;

    public string CreateButtonText => IsAssemblyMode ? "Create Road Assembly" : IsToposolidOutput ? "Create Toposolid" : "Create Road";

    // ---- single profile or assembly -----------------------------------------------------------------------------

    public List<RoadChoice<SweepMode>> Modes { get; } = new()
    {
        new RoadChoice<SweepMode>(SweepMode.Profile, "Single profile"),
        new RoadChoice<SweepMode>(SweepMode.Assembly, "Assembly of profiles")
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAssemblyMode))]
    [NotifyPropertyChangedFor(nameof(IsProfileMode))]
    [NotifyPropertyChangedFor(nameof(ShowCategory))]
    [NotifyPropertyChangedFor(nameof(IsToposolidOutput))]
    [NotifyPropertyChangedFor(nameof(IsRoadOutput))]
    [NotifyPropertyChangedFor(nameof(CreateButtonText))]
    [NotifyPropertyChangedFor(nameof(ExistingRoadNote))]
    [NotifyPropertyChangedFor(nameof(ShowGradeTopo))]
    [NotifyPropertyChangedFor(nameof(ShowGradeTopoPick))]
    [NotifyPropertyChangedFor(nameof(GradeTopoStatus))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private RoadChoice<SweepMode>? _selectedMode;

    public bool IsAssemblyMode => SelectedMode?.Value == SweepMode.Assembly;
    public bool IsProfileMode => !IsAssemblyMode;

    /// <summary>A single profile is filed under one category; the parts of an assembly each have their own.</summary>
    public bool ShowCategory => IsProfileMode && IsRoadOutput;

    [ObservableProperty]
    private List<SweepAssembly> _assemblies = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AssemblyStatus))]
    [NotifyPropertyChangedFor(nameof(GradeTopoStatus))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private SweepAssembly? _selectedAssembly;

    // ---- grading the topo out from the sides of an assembly -----------------------------------------------------

    /// <summary>Grading the ground needs a toposolid, which needs Revit 2024 or later.</summary>
    public bool CanGradeTopo =>
#if REVIT2024_OR_GREATER
        true;
#else
        false;
#endif

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGradeTopoPick))]
    [NotifyPropertyChangedFor(nameof(GradeTopoStatus))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _gradeTopo;

    public bool ShowGradeTopo => IsAssemblyMode && CanGradeTopo;

    public bool ShowGradeTopoPick => ShowGradeTopo && GradeTopo;

    private readonly List<Element> _topographies = new();

    /// <summary>The toposolids that will be graded.</summary>
    public IReadOnlyList<Element> Topographies => _topographies;

    /// <summary>What the toposolid choice means for the chosen assembly, or what to do next.</summary>
    public string GradeTopoStatus
    {
        get
        {
            if (!ShowGradeTopo || !GradeTopo)
            {
                return string.Empty;
            }

            var text = _topographies.Count switch
            {
                0 => "No toposolid picked. Pick every toposolid either side of the road that should be graded.",
                1 => $"Toposolid: {Describe(_topographies[0])}.",
                _ => $"Toposolids ({_topographies.Count}): {string.Join(", ", _topographies.Select(Describe))}."
            };
            if (SelectedAssembly is { } assembly && !assembly.Left.Enabled && !assembly.Right.Enabled)
            {
                text += " This assembly has no side grading turned on: choose Edit assemblies to set cut and fill slopes.";
            }

            return text;
        }
    }

    /// <summary>True when the dialog was hidden to pick a toposolid rather than an alignment (with <see cref="PickRequested"/>).</summary>
    public bool PickToposolidRequested { get; set; }

    [RelayCommand]
    private void PickToposolid()
    {
        PickRequested = true;
        PickToposolidRequested = true;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Lets the user click one or more toposolids (Finish to accept) and replaces the earlier choice with them.
    /// Called by the command while the dialog is hidden.
    /// </summary>
    internal void PickToposolidElement()
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var references = uidoc.Selection.PickObjects(ObjectType.Element, new TopoPickFilter(),
                "Select the toposolids to grade out from the road, then choose Finish");
            SelectToposolids(references.Select(reference => App.RevitDocument.GetElement(reference)));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting a toposolid");
            TaskDialog.Show("Sweep Along Alignment", ex.Message);
        }
    }

    /// <summary>Adds a toposolid to those to grade; one already chosen is not added twice.</summary>
    internal void SelectToposolid(Element element)
    {
        if (element is not null && _topographies.All(t => t.Id != element.Id))
        {
            _topographies.Add(element);
        }

        TopographiesChanged();
    }

    /// <summary>Makes these the toposolids to grade, in place of any chosen before.</summary>
    internal void SelectToposolids(IEnumerable<Element?> elements)
    {
        _topographies.Clear();
        foreach (var element in elements)
        {
            if (element is not null && _topographies.All(t => t.Id != element.Id))
            {
                _topographies.Add(element);
            }
        }

        TopographiesChanged();
    }

    /// <summary>The name of a picked toposolid with its id, since toposolids of one type all share a name.</summary>
    private static string Describe(Element element) => element.IsValidObject ? $"{element.Name} (Id {element.Id})" : "(removed from the model)";

    private void TopographiesChanged()
    {
        OnPropertyChanged(nameof(GradeTopoStatus));
        OnPropertyChanged(nameof(Topographies));
        CreateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>What the chosen assembly holds, or what to do if there is none.</summary>
    public string AssemblyStatus
    {
        get
        {
            if (Assemblies.Count == 0)
            {
                return "No assemblies yet. Choose Edit assemblies to make one.";
            }

            if (SelectedAssembly is null)
            {
                return string.Empty;
            }

            var missing = SelectedAssembly.Parts
                .Where(p => _library.Get(p.ProfileName) is null)
                .Select(p => string.IsNullOrWhiteSpace(p.Name) ? p.ProfileName : p.Name)
                .ToList();
            var count = SelectedAssembly.Parts.Count;
            var text = $"{count} part{(count == 1 ? string.Empty : "s")}";
            return missing.Count == 0 ? text + "." : $"{text}. Profile missing or unreadable for: {string.Join(", ", missing)}.";
        }
    }

    /// <summary>Raised when the assembly editor should be shown; the view owns the window.</summary>
    public event EventHandler? EditAssembliesRequested;

    [RelayCommand]
    private void EditAssemblies() => EditAssembliesRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The editor for the assemblies, started on the selected one.</summary>
    internal AssemblyEditorViewModel CreateEditor()
    {
        return new AssemblyEditorViewModel(_library, _assemblyStore, Categories, DisplayUnits, SelectedDisplayUnit, SelectedAssembly?.Name);
    }

    /// <summary>Takes up what the editor changed: reloads the list and selects the assembly last saved.</summary>
    internal void ApplyEditor(AssemblyEditorViewModel editor)
    {
        if (!editor.Changed)
        {
            return;
        }

        LoadAssemblies(editor.SavedName ?? SelectedAssembly?.Name);
    }

    private void LoadAssemblies(string? select)
    {
        Assemblies = _assemblyStore.LoadAll();
        SelectedAssembly = Assemblies.FirstOrDefault(a => string.Equals(a.Name, select, StringComparison.OrdinalIgnoreCase))
                           ?? Assemblies.FirstOrDefault();
        OnPropertyChanged(nameof(AssemblyStatus));
    }

    [ObservableProperty]
    private string _alignmentStatus = "No alignment selected.";

    [ObservableProperty]
    private List<ProfileTypeInfo> _profileTypes = new();

    [ObservableProperty]
    private ProfileTypeInfo? _selectedProfileType;

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
    private RoadCategory? _selectedCategory;

    private bool _hasExistingRoad;

    /// <summary>Warns that creating again replaces the road already made for the alignment; not shown when only a toposolid is made, which leaves roads alone.</summary>
    public string ExistingRoadNote => _hasExistingRoad && !IsToposolidOutput
        ? "A road already exists for this alignment. Creating again replaces it."
        : string.Empty;

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
    public SweepAlignmentRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly ProfileLibrary _library;
    private readonly AssemblyStore _assemblyStore;
    private readonly Dictionary<long, (SectionProfile? Profile, string? Error)> _profileCache = new();
    private Element? _alignment;
    private string _alignmentName = string.Empty;
    private SectionProfile? _profile;

    public SweepAlignmentViewModel() : this(new AssemblyStore())
    {
    }

    /// <param name="assemblyStore">Where the assemblies are kept; tests point it at a temporary folder.</param>
    internal SweepAlignmentViewModel(AssemblyStore assemblyStore)
    {
        _assemblyStore = assemblyStore;
        _library = new ProfileLibrary(App.RevitDocument);

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

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == documentUnits) ?? DisplayUnits.First();

        Categories = RoadBuilder.GetCategories(App.RevitDocument);
        FillToposolidChoices();
        ProfileTypes = _library.Types;
        ProfileStatus = ProfileTypes.Count == 0
            ? "No profile families are loaded. Load one from Revit's Profiles library, or draw your own closed shape in a profile family."
            : string.Empty;

        LoadSettings();
    }

    public bool HasAlignment => _alignment is not null;

    private bool CanCreate()
    {
        if (!HasAlignment || Interval <= 0m)
        {
            return false;
        }

        if (IsAssemblyMode)
        {
            return SelectedAssembly is { Parts.Count: > 0 } && (!ShowGradeTopo || !GradeTopo || _topographies.Count > 0);
        }

        return _profile is not null
               && (!IsToposolidOutput || (SelectedToposolidType is not null && SelectedLevel is not null));
    }

    /// <summary>Lists the toposolid types and levels. There are no toposolids before Revit 2024, so the lists stay empty there.</summary>
    private void FillToposolidChoices()
    {
#if REVIT2024_OR_GREATER
        ToposolidTypes.AddRange(ToposolidBuilder.GetTypes(App.RevitDocument).Select(type => new RoadChoice<ElementId>(type.Id, type.Name)));
        Levels.AddRange(ToposolidBuilder.GetLevels(App.RevitDocument).Select(level => new RoadChoice<ElementId>(level.Id, level.Name)));
#endif
    }

    /// <summary>Starts the level on the highest one at or below the alignment, which is where the toposolid will sit.</summary>
    private void SelectLevelFor(Element alignment)
    {
#if REVIT2024_OR_GREATER
        var levels = ToposolidBuilder.GetLevels(App.RevitDocument);
        var level = ToposolidBuilder.DefaultLevel(levels, alignment);
        if (level is not null)
        {
            SelectedLevel = Levels.FirstOrDefault(choice => choice.Value == level.Id) ?? SelectedLevel;
        }
#endif
    }

    private double ToInternal(decimal displayValue)
    {
        return ((double)displayValue).FromUnit(SelectedDisplayUnit.DisplayUnit);
    }

    private decimal ToDisplay(decimal internalValue)
    {
        return decimal.Round((decimal)((double)internalValue).ToUnit(SelectedDisplayUnit.DisplayUnit), 3);
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
        _hasExistingRoad = false;
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

            _hasExistingRoad = RoadBuilder.FindRoads(App.RevitDocument, element).Count > 0;
            SelectLevelFor(element);

            return true;
        }
        finally
        {
            OnPropertyChanged(nameof(HasAlignment));
            OnPropertyChanged(nameof(ExistingRoadNote));
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- the cross-section --------------------------------------------------------------------------------------

    partial void OnSelectedProfileTypeChanged(ProfileTypeInfo? value) => LoadProfile(value);

    partial void OnMirrorProfileChanged(bool value) => RefreshProfileStatus();

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel value)
    {
        if (oldValue is not null)
        {
            Interval = Helpers.UnitConversion.Convert(Interval, oldValue.DisplayUnit, value.DisplayUnit);
            VerticalOffset = Helpers.UnitConversion.Convert(VerticalOffset, oldValue.DisplayUnit, value.DisplayUnit);
        }

        RefreshProfileStatus();
    }

    /// <summary>Reads the shape drawn in the chosen profile family (once per type) and shows its size.</summary>
    private void LoadProfile(ProfileTypeInfo? type)
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
            ProfileStatus = cached.Error ?? string.Empty;
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
        if (_alignment is null)
        {
            return;
        }

        if (IsAssemblyMode)
        {
            CreateAssembly();
            return;
        }

        if (_profile is null)
        {
            return;
        }

        try
        {
            var options = new RoadOptions
            {
                Interval = ToInternal(Interval),
                VerticalOffset = ToInternal(VerticalOffset),
                Category = SelectedCategory?.Category ?? BuiltInCategory.OST_GenericModel,
                ProfileName = SelectedProfileType?.Name ?? string.Empty,
                GeometryMethod = SelectedGeometryMethod?.Value ?? RoadGeometryMethod.SegmentedLoft,
                Output = SelectedOutput?.Value ?? RoadOutput.DirectShape
            };

            if (options.Output == RoadOutput.Toposolid && SelectedToposolidType is not null && SelectedLevel is not null)
            {
                options.ToposolidTypeId = SelectedToposolidType.Value;
                options.LevelId = SelectedLevel.Value;
            }

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

    /// <summary>Checks every part's profile can be read, then hands the assembly to the command. Nothing is built here.</summary>
    private void CreateAssembly()
    {
        if (_alignment is null || SelectedAssembly is null)
        {
            return;
        }

        try
        {
            var profiles = new Dictionary<string, SectionProfile>(StringComparer.OrdinalIgnoreCase);
            var problems = new List<string>();
            foreach (var part in SelectedAssembly.Parts)
            {
                var name = string.IsNullOrWhiteSpace(part.Name) ? part.ProfileName : part.Name;
                var profile = _library.Get(part.ProfileName, out var error);
                if (profile is null)
                {
                    problems.Add($"{name}: {error}");
                }
                else
                {
                    profiles[part.ProfileName] = profile;
                }
            }

            if (problems.Count > 0)
            {
                TaskDialog.Show("Sweep Along Alignment",
                    "The assembly can't be built until these are fixed. Choose Edit assemblies to change the profiles.\n\n" + string.Join("\n", problems));
                return;
            }

            var options = new RoadOptions
            {
                Interval = ToInternal(Interval),
                VerticalOffset = ToInternal(VerticalOffset),
                ProfileName = SelectedAssembly.Name,
                GeometryMethod = SelectedGeometryMethod?.Value ?? RoadGeometryMethod.SegmentedLoft,
                Output = RoadOutput.DirectShape
            };

            SaveSettings();

            Request = new SweepAlignmentRequest(_alignment, _alignmentName, null, options)
            {
                Assembly = SelectedAssembly,
                AssemblyProfiles = profiles,
                Topographies = ShowGradeTopo && GradeTopo ? _topographies.ToList() : new List<Element>()
            };
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Sweep of an assembly along an alignment failed");
            TaskDialog.Show("Sweep Along Alignment", ex.Message);
        }
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        SelectedMode = Modes.FirstOrDefault(m => m.Value.ToString() == _settings.SweepMode) ?? Modes[0];
        LoadAssemblies(_settings.SweepAssemblyName);

        Interval = ToDisplay(_settings.SweepInterval);
        VerticalOffset = ToDisplay(_settings.SweepVerticalOffset);
        MirrorProfile = _settings.SweepMirrorProfile;
        GradeTopo = _settings.SweepGradeTopo;

        SelectedCategory = Categories.FirstOrDefault(c => c.Category.ToString() == _settings.SweepCategory) ?? Categories.FirstOrDefault();
        SelectedProfileType = ProfileTypes.FirstOrDefault(t => t.Name == _settings.SweepProfileName) ?? ProfileTypes.FirstOrDefault();
        SelectedGeometryMethod = GeometryMethods.FirstOrDefault(m => m.Value.ToString() == _settings.SweepGeometryMethod) ?? GeometryMethods[0];
        SelectedOutput = Outputs.FirstOrDefault(o => o.Value.ToString() == _settings.SweepOutput) ?? Outputs[0];
        SelectedToposolidType = ToposolidTypes.FirstOrDefault(t => t.Name == _settings.RoadToposolidType) ?? ToposolidTypes.FirstOrDefault();
        SelectedLevel = Levels.FirstOrDefault();
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.SweepMode = (SelectedMode?.Value ?? SweepMode.Profile).ToString();
        if (SelectedAssembly is not null)
        {
            _settings.SweepAssemblyName = SelectedAssembly.Name;
        }

        _settings.SweepInterval = (decimal)ToInternal(Interval);
        _settings.SweepVerticalOffset = (decimal)ToInternal(VerticalOffset);
        _settings.SweepMirrorProfile = MirrorProfile;
        _settings.SweepGradeTopo = GradeTopo;
        _settings.SweepCategory = SelectedCategory?.Category.ToString() ?? string.Empty;
        _settings.SweepProfileName = SelectedProfileType?.Name ?? string.Empty;
        _settings.SweepGeometryMethod = (SelectedGeometryMethod?.Value ?? RoadGeometryMethod.SegmentedLoft).ToString();
        _settings.SweepOutput = (SelectedOutput?.Value ?? RoadOutput.DirectShape).ToString();
        if (SelectedToposolidType is not null)
        {
            _settings.RoadToposolidType = SelectedToposolidType.Name;
        }
        _settings.SaveSettings();
    }
}