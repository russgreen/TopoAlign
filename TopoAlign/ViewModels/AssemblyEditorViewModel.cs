using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>One row of the parts grid: a part of the assembly with its values as the user types them (display units).</summary>
internal sealed partial class AssemblyPartRow : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ProfileTypeInfo? _selectedProfile;

    /// <summary>
    /// The profile the part was saved with. Kept even when the type isn't loaded in this project, so saving the assembly
    /// again doesn't lose it.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _lateralOffset;

    [ObservableProperty]
    private decimal _verticalOffset;

    [ObservableProperty]
    private bool _mirrorProfile;

    [ObservableProperty]
    private RoadCategory? _selectedCategory;

    partial void OnSelectedProfileChanged(ProfileTypeInfo? value)
    {
        if (value is not null)
        {
            ProfileName = value.Name;
        }
    }
}

/// <summary>One side's topo grading as the user types it: slopes in the chosen slope mode, the widest run in display units.</summary>
internal sealed partial class SideGradingRow : ObservableObject
{
    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private decimal _cutValue = 2m;

    [ObservableProperty]
    private decimal _fillValue = 3m;

    [ObservableProperty]
    private decimal _maxWidth;
}

/// <summary>A way of entering a slope, with the name the editor shows for it.</summary>
internal sealed class SlopeModeOption
{
    public SlopeModeOption(TopoAlign.Grading.SlopeMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public TopoAlign.Grading.SlopeMode Mode { get; }
    public string Label { get; }
}

internal partial class AssemblyEditorViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<AssemblyEditorViewModel>();

    private readonly ProfileLibrary _library;
    private readonly AssemblyStore _store;
    private bool _loading;

    public AssemblyEditorViewModel(ProfileLibrary library, AssemblyStore store, List<RoadCategory> categories,
        List<Models.DisplayUnitModel> displayUnits, Models.DisplayUnitModel selectedUnit, string? assemblyName)
    {
        _library = library;
        _store = store;
        Categories = categories;
        ProfileTypes = library.Types;
        DisplayUnits = displayUnits;
        _selectedDisplayUnit = selectedUnit;

        Parts.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (AssemblyPartRow row in e.NewItems)
                {
                    row.PropertyChanged += (_, _) => OnPartChanged();
                }
            }

            OnPartChanged();
        };

        // the dashed slope lines follow the grading values
        LeftGrading.PropertyChanged += (_, _) => OnPartChanged();
        RightGrading.PropertyChanged += (_, _) => OnPartChanged();

        ReloadNames();
        var existing = assemblyName is not null && AssemblyNames.Contains(assemblyName) ? assemblyName : AssemblyNames.FirstOrDefault();
        if (existing is not null)
        {
            SelectedAssemblyName = existing;
        }
        else
        {
            NewAssembly();
        }
    }

    public List<RoadCategory> Categories { get; }
    public List<ProfileTypeInfo> ProfileTypes { get; }
    public List<Models.DisplayUnitModel> DisplayUnits { get; }

    public ObservableCollection<string> AssemblyNames { get; } = new();
    public ObservableCollection<AssemblyPartRow> Parts { get; } = new();

    // ---- grading the ground out from the sides ------------------------------------------------------------------

    /// <summary>Grading the toposolid needs Revit 2024 or later; before that the editor hides the settings.</summary>
    public bool CanGradeTopo =>
#if REVIT2024_OR_GREATER
        true;
#else
        false;
#endif

    public SideGradingRow LeftGrading { get; } = new();
    public SideGradingRow RightGrading { get; } = new();

    public List<SlopeModeOption> SlopeModes { get; } = new()
    {
        new SlopeModeOption(TopoAlign.Grading.SlopeMode.Ratio, "Ratio (1 : N)"),
        new SlopeModeOption(TopoAlign.Grading.SlopeMode.Percent, "Percent (%)"),
        new SlopeModeOption(TopoAlign.Grading.SlopeMode.Degrees, "Degrees (°)")
    };

    [ObservableProperty]
    private SlopeModeOption? _selectedSlopeMode;

    partial void OnSelectedSlopeModeChanged(SlopeModeOption? value) => OnPartChanged();

    private void LoadGrading(SweepAssembly assembly)
    {
        var left = assembly.Left ?? new SideGrading();
        var right = assembly.Right ?? new SideGrading();

        SelectedSlopeMode = SlopeModes.FirstOrDefault(m => m.Mode == (left.Enabled || !right.Enabled ? left.SlopeMode : right.SlopeMode)) ?? SlopeModes[0];
        FillRow(LeftGrading, left);
        FillRow(RightGrading, right);
    }

    private void FillRow(SideGradingRow row, SideGrading grading)
    {
        row.Enabled = grading.Enabled;
        row.CutValue = (decimal)grading.CutValue;
        row.FillValue = (decimal)grading.FillValue;
        row.MaxWidth = FromMillimetres(grading.MaxWidthMm);
    }

    private SideGrading ToGrading(SideGradingRow row) => new()
    {
        Enabled = row.Enabled,
        SlopeMode = (SelectedSlopeMode ?? SlopeModes[0]).Mode,
        CutValue = (double)row.CutValue,
        FillValue = (double)row.FillValue,
        MaxWidthMm = ToMillimetres(row.MaxWidth)
    };

    /// <summary>The name of the assembly last saved, so the sweep dialog can select it. Null until something is saved.</summary>
    public string? SavedName { get; private set; }

    /// <summary>True when an assembly was saved or deleted, so the sweep dialog should reload its list.</summary>
    public bool Changed { get; private set; }

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteAssemblyCommand))]
    private string? _selectedAssemblyName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _assemblyName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemovePartCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private AssemblyPartRow? _selectedPart;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _warning = string.Empty;

    [ObservableProperty]
    private AssemblyPreviewResult _preview = new();

    /// <summary>The picture of the assembly changed; the view draws it again.</summary>
    public event EventHandler? PreviewChanged;

    /// <summary>The selected row changed; the view highlights its outline.</summary>
    public int SelectedIndex => SelectedPart is null ? -1 : Parts.IndexOf(SelectedPart);

    // ---- choosing and editing -----------------------------------------------------------------------------------

    private void ReloadNames()
    {
        AssemblyNames.Clear();
        foreach (var assembly in _store.LoadAll())
        {
            AssemblyNames.Add(assembly.Name);
        }
    }

    partial void OnSelectedAssemblyNameChanged(string? value)
    {
        if (value is null)
        {
            return;
        }

        var assembly = _store.LoadAll().FirstOrDefault(a => string.Equals(a.Name, value, StringComparison.OrdinalIgnoreCase));
        if (assembly is not null)
        {
            Load(assembly);
        }
    }

    private void Load(SweepAssembly assembly)
    {
        _loading = true;
        try
        {
            AssemblyName = assembly.Name;
            Parts.Clear();
            foreach (var part in assembly.Parts)
            {
                Parts.Add(ToRow(part));
            }

            LoadGrading(assembly);
        }
        finally
        {
            _loading = false;
        }

        SelectedPart = Parts.FirstOrDefault();
        Status = string.Empty;
        OnPartChanged();
    }

    partial void OnSelectedPartChanged(AssemblyPartRow? value)
    {
        OnPropertyChanged(nameof(SelectedIndex));
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || oldValue == newValue)
        {
            return;
        }

        _loading = true;
        try
        {
            foreach (var row in Parts)
            {
                row.LateralOffset = Helpers.UnitConversion.Convert(row.LateralOffset, oldValue.DisplayUnit, newValue.DisplayUnit);
                row.VerticalOffset = Helpers.UnitConversion.Convert(row.VerticalOffset, oldValue.DisplayUnit, newValue.DisplayUnit);
            }

            LeftGrading.MaxWidth = Helpers.UnitConversion.Convert(LeftGrading.MaxWidth, oldValue.DisplayUnit, newValue.DisplayUnit);
            RightGrading.MaxWidth = Helpers.UnitConversion.Convert(RightGrading.MaxWidth, oldValue.DisplayUnit, newValue.DisplayUnit);
        }
        finally
        {
            _loading = false;
        }

        OnPartChanged();
    }

    private void OnPartChanged()
    {
        if (_loading)
        {
            return;
        }

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var parts = Parts.Select(ToPart).ToList();
        var problems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var preview = AssemblyPreview.Build(parts, name =>
        {
            var profile = _library.Get(name, out var error);
            if (profile is null && error is not null)
            {
                problems[name] = error;
            }

            return profile;
        }, CanGradeTopo ? ToGrading(LeftGrading) : null, CanGradeTopo ? ToGrading(RightGrading) : null);

        Preview = preview;
        Warning = problems.Count == 0
            ? (preview.Missing.Count == 0 ? string.Empty : $"Not drawn, no profile chosen: {string.Join(", ", preview.Missing)}.")
            : string.Join(" ", problems.Values);

        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- converting between rows and the saved parts ------------------------------------------------------------

    private double ToMillimetres(decimal displayValue)
    {
        var feet = UnitUtils.ConvertToInternalUnits((double)displayValue, SelectedDisplayUnit.DisplayUnit);
        return feet * AssemblyPart.MillimetresPerFoot;
    }

    private decimal FromMillimetres(double millimetres)
    {
        var display = UnitUtils.ConvertFromInternalUnits(millimetres / AssemblyPart.MillimetresPerFoot, SelectedDisplayUnit.DisplayUnit);
        return decimal.Round((decimal)display, 3);
    }

    private AssemblyPart ToPart(AssemblyPartRow row) => new()
    {
        Name = row.Name?.Trim() ?? string.Empty,
        ProfileName = row.ProfileName,
        LateralOffsetMm = ToMillimetres(row.LateralOffset),
        VerticalOffsetMm = ToMillimetres(row.VerticalOffset),
        MirrorProfile = row.MirrorProfile,
        Category = row.SelectedCategory?.Category.ToString() ?? string.Empty
    };

    private AssemblyPartRow ToRow(AssemblyPart part)
    {
        var row = new AssemblyPartRow
        {
            Name = part.Name,
            ProfileName = part.ProfileName,
            SelectedProfile = ProfileTypes.FirstOrDefault(t => string.Equals(t.Name, part.ProfileName, StringComparison.OrdinalIgnoreCase)),
            LateralOffset = FromMillimetres(part.LateralOffsetMm),
            VerticalOffset = FromMillimetres(part.VerticalOffsetMm),
            MirrorProfile = part.MirrorProfile,
            SelectedCategory = Categories.FirstOrDefault(c => c.Category.ToString() == part.Category) ?? Categories.FirstOrDefault()
        };

        // a saved profile that isn't loaded keeps its name; the setter above only runs for a type that was found
        row.ProfileName = part.ProfileName;
        return row;
    }

    /// <summary>The assembly as it stands in the dialog.</summary>
    internal SweepAssembly ToAssembly() => new()
    {
        Name = AssemblyName.Trim(),
        Parts = Parts.Select(ToPart).ToList(),
        Left = ToGrading(LeftGrading),
        Right = ToGrading(RightGrading)
    };

    // ---- commands -----------------------------------------------------------------------------------------------

    [RelayCommand]
    private void NewAssembly()
    {
        _loading = true;
        try
        {
            SelectedAssemblyName = null;
            AssemblyName = string.Empty;
            Parts.Clear();
            LoadGrading(new SweepAssembly());
        }
        finally
        {
            _loading = false;
        }

        Status = string.Empty;
        AddPart();
    }

    [RelayCommand]
    private void AddPart()
    {
        var row = new AssemblyPartRow
        {
            Name = $"Part {Parts.Count + 1}",
            SelectedCategory = Categories.FirstOrDefault(),
            SelectedProfile = Parts.LastOrDefault()?.SelectedProfile
        };

        Parts.Add(row);
        SelectedPart = row;
    }

    private bool HasSelectedPart() => SelectedPart is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedPart))]
    private void RemovePart()
    {
        if (SelectedPart is null)
        {
            return;
        }

        var index = Parts.IndexOf(SelectedPart);
        Parts.Remove(SelectedPart);
        SelectedPart = Parts.Count == 0 ? null : Parts[Math.Min(index, Parts.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPart))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(HasSelectedPart))]
    private void MoveDown() => Move(1);

    private void Move(int direction)
    {
        if (SelectedPart is null)
        {
            return;
        }

        var row = SelectedPart;
        var from = Parts.IndexOf(row);
        var to = from + direction;
        if (to < 0 || to >= Parts.Count)
        {
            return;
        }

        Parts.Move(from, to);
        SelectedPart = row;
        OnPropertyChanged(nameof(SelectedIndex));
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(AssemblyName);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Parts.Count == 0)
        {
            Status = "Add at least one part first.";
            return;
        }

        var missing = Parts.Where(p => string.IsNullOrWhiteSpace(p.ProfileName)).ToList();
        if (missing.Count > 0)
        {
            Status = $"Choose a profile for: {string.Join(", ", missing.Select(p => string.IsNullOrWhiteSpace(p.Name) ? "unnamed part" : p.Name))}.";
            return;
        }

        try
        {
            var assembly = ToAssembly();

            foreach (var (name, grading) in new[] { ("Left", assembly.Left), ("Right", assembly.Right) })
            {
                if (!grading.Enabled)
                {
                    continue;
                }

                try
                {
                    grading.CutRiseOverRun();
                    grading.FillRiseOverRun();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    // the exception adds the parameter name, on a second line or in brackets, depending on the framework
                    var reason = ex.Message.Split('\n')[0].Trim();
                    var bracket = reason.IndexOf(" (Parameter", StringComparison.Ordinal);
                    Status = $"{name} grading: {(bracket > 0 ? reason.Substring(0, bracket) : reason)}";
                    return;
                }

                if (grading.MaxWidthMm <= 0.0)
                {
                    Status = $"{name} grading: the maximum width must be greater than zero.";
                    return;
                }
            }

            _store.Save(assembly);
            SavedName = assembly.Name;
            Changed = true;

            ReloadNames();
            _loading = true;
            try
            {
                SelectedAssemblyName = AssemblyNames.FirstOrDefault(n => string.Equals(n, assembly.Name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _loading = false;
            }

            Status = $"Saved '{assembly.Name}'.";
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not save the assembly");
            Status = $"Could not save: {ex.Message}";
        }
    }

    private bool CanDelete() => SelectedAssemblyName is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteAssembly()
    {
        if (SelectedAssemblyName is null)
        {
            return;
        }

        try
        {
            var name = SelectedAssemblyName;
            _store.Delete(name);
            Changed = true;
            if (string.Equals(SavedName, name, StringComparison.OrdinalIgnoreCase))
            {
                SavedName = null;
            }

            ReloadNames();
            var next = AssemblyNames.FirstOrDefault();
            if (next is not null)
            {
                SelectedAssemblyName = next;
            }
            else
            {
                NewAssembly();
            }

            Status = $"Deleted '{name}'.";
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not delete the assembly");
            Status = $"Could not delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Close() => OnClosingRequest();
}
