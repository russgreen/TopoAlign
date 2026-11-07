using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>What the Kerb dialog hands back to the command.</summary>
internal sealed class KerbRequest
{
    public KerbRequest(IReadOnlyList<KerbChain> chains, SectionProfile profile, KerbOptions options, bool flipped, bool review)
    {
        Chains = chains;
        Profile = profile;
        Options = options;
        Flipped = flipped;
        Review = review;
    }

    /// <summary>True when the profile was flipped left to right in the dialog.</summary>
    public bool Flipped { get; }

    /// <summary>True when the kerb should be shown in the model for the user to keep, flip or cancel before it is committed.</summary>
    public bool Review { get; }

    public IReadOnlyList<KerbChain> Chains { get; }

    /// <summary>The cross-section, already mirrored if the user asked for that.</summary>
    public SectionProfile Profile { get; }

    public KerbOptions Options { get; }
}

/// <summary>What the dialog was hidden to let the user pick.</summary>
internal enum KerbSweepPickKind
{
    None,
    Edges,
    Lines
}

internal partial class KerbViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<KerbViewModel>();

    private const string Title = "Kerb";

    public List<RoadChoice<RoadOutput>> Outputs { get; } = new()
    {
        new RoadChoice<RoadOutput>(RoadOutput.DirectShape, "Direct shape"),
        new RoadChoice<RoadOutput>(RoadOutput.Family, "Family")
    };

    [ObservableProperty]
    private RoadChoice<RoadOutput>? _selectedOutput;

    [ObservableProperty]
    private string _pickStatus = "No edges or lines picked.";

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
    private bool _flipProfile;

    [ObservableProperty]
    private bool _reviewBeforeKeeping = true;

    [ObservableProperty]
    private List<RoadCategory> _categories = new();

    [ObservableProperty]
    private RoadCategory? _selectedCategory;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>What to pick while the dialog is hidden; <see cref="KerbSweepPickKind.None"/> when it was closed instead.</summary>
    public KerbSweepPickKind PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public KerbRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly Dictionary<long, (SectionProfile? Profile, string? Error)> _profileCache = new();
    private readonly List<Curve> _curves = new();
    private SectionProfile? _profile;

    public KerbViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Kerb {informationVersion}";

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
        ProfileTypes = SectionProfileReader.ListProfileTypes(App.RevitDocument);
        ProfileStatus = ProfileTypes.Count == 0
            ? "No profile families are loaded. Load one from Revit's Profiles library, or draw your own closed shape in a profile family."
            : string.Empty;

        LoadSettings();
    }

    private bool CanCreate() => _curves.Count > 0 && _profile is not null && Interval > 0m;

    private double ToInternal(decimal displayValue)
    {
        return ((double)displayValue).FromUnit(SelectedDisplayUnit.DisplayUnit);
    }

    private decimal ToDisplay(decimal internalValue)
    {
        return decimal.Round((decimal)((double)internalValue).ToUnit(SelectedDisplayUnit.DisplayUnit), 3);
    }

    // ---- choosing the path --------------------------------------------------------------------------------------

    [RelayCommand]
    private void PickEdges() => RequestPick(KerbSweepPickKind.Edges);

    [RelayCommand]
    private void PickLines() => RequestPick(KerbSweepPickKind.Lines);

    private void RequestPick(KerbSweepPickKind kind)
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PickRequested = kind;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearPicks()
    {
        _curves.Clear();
        RefreshPickStatus();
    }

    /// <summary>Lets the user click edges or model lines. Called by the command while the dialog is hidden.</summary>
    internal void RunPick(KerbSweepPickKind kind)
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            if (kind == KerbSweepPickKind.Edges)
            {
                foreach (var reference in uidoc.Selection.PickObjects(ObjectType.Edge, "Select the edges to run the kerb along, then click Finish"))
                {
                    var element = App.RevitDocument.GetElement(reference.ElementId);
                    var edge = element.GetGeometryObjectFromReference(reference) as Edge
                               ?? throw new InvalidOperationException("The reference is not an edge.");
                    var curve = edge.AsCurve();
                    if (element is FamilyInstance instance && JoinGeometryUtils.GetJoinedElements(App.RevitDocument, instance).Count == 0)
                    {
                        curve = curve.CreateTransformed(instance.GetTransform());
                    }

                    _curves.Add(curve);
                }
            }
            else
            {
                AddModelCurves(uidoc.Selection.PickObjects(ObjectType.Element, new LinePickFilter(), "Select the model lines to run the kerb along, then click Finish")
                    .Select(reference => App.RevitDocument.GetElement(reference)));
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while picking kerb edges");
            TaskDialog.Show(Title, ex.Message);
        }
        finally
        {
            RefreshPickStatus();
        }
    }

    /// <summary>Adds the curves of model lines. Returns how many were added.</summary>
    internal int AddModelCurves(IEnumerable<Element> elements)
    {
        var added = 0;
        foreach (var element in elements)
        {
            if (element is ModelCurve { GeometryCurve: { IsBound: true } curve })
            {
                _curves.Add(curve);
                added++;
            }
        }

        RefreshPickStatus();
        return added;
    }

    private void RefreshPickStatus()
    {
        if (_curves.Count == 0)
        {
            PickStatus = "No edges or lines picked.";
        }
        else
        {
            var chains = SweepKerbPath.Build(_curves, ToInternal(Interval), out var error);
            if (error is not null)
            {
                PickStatus = $"{_curves.Count} picked. {error}";
            }
            else
            {
                var length = UnitUtils.ConvertFromInternalUnits(chains.Sum(c => c.Length), SelectedDisplayUnit.DisplayUnit);
                var runs = chains.Count;
                PickStatus = $"{SweepKerbPath.RemoveDuplicates(_curves).Count} edge(s) in {runs} run{(runs == 1 ? string.Empty : "s")}, " +
                             $"{length:N2} {SelectedDisplayUnit.Label} long.";
            }
        }

        CreateCommand.NotifyCanExecuteChanged();
    }

    // ---- the cross-section --------------------------------------------------------------------------------------

    partial void OnSelectedProfileTypeChanged(ProfileTypeInfo? value) => LoadProfile(value);

    partial void OnFlipProfileChanged(bool value) => RefreshProfileStatus();

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel value)
    {
        if (oldValue is not null)
        {
            Interval = Helpers.UnitConversion.Convert(Interval, oldValue.DisplayUnit, value.DisplayUnit);
            VerticalOffset = Helpers.UnitConversion.Convert(VerticalOffset, oldValue.DisplayUnit, value.DisplayUnit);
        }

        RefreshProfileStatus();
        if (_curves.Count > 0)
        {
            RefreshPickStatus();
        }
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
        ProfileStatus = $"Profile {width:N2} wide by {height:N2} high ({SelectedDisplayUnit.Label}), on the " +
                        (FlipProfile ? "left" : "right") + " of the direction of the first edge.";
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
        if (_curves.Count == 0 || _profile is null)
        {
            return;
        }

        try
        {
            var options = new KerbOptions
            {
                Interval = ToInternal(Interval),
                VerticalOffset = ToInternal(VerticalOffset),
                Category = SelectedCategory?.Category ?? BuiltInCategory.OST_GenericModel,
                ProfileName = SelectedProfileType?.Name ?? string.Empty,
                Output = SelectedOutput?.Value ?? RoadOutput.DirectShape
            };

            var chains = SweepKerbPath.Build(_curves, options.Interval, out var error);
            if (error is not null || chains.Count == 0)
            {
                TaskDialog.Show(Title, error ?? "The picked edges could not be followed.");
                return;
            }

            SaveSettings();

            // The kerb is built by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new KerbRequest(chains, FlipProfile ? _profile.Mirrored() : _profile, options, FlipProfile, ReviewBeforeKeeping);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Kerb failed");
            TaskDialog.Show(Title, ex.Message);
        }
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        Interval = ToDisplay(_settings.KerbSweepInterval);
        VerticalOffset = ToDisplay(_settings.KerbSweepVerticalOffset);
        FlipProfile = _settings.KerbSweepFlip;
        ReviewBeforeKeeping = _settings.KerbSweepReview;

        SelectedCategory = Categories.FirstOrDefault(c => c.Category.ToString() == _settings.KerbSweepCategory) ?? Categories.FirstOrDefault();
        SelectedProfileType = ProfileTypes.FirstOrDefault(t => t.Name == _settings.KerbSweepProfileName) ?? ProfileTypes.FirstOrDefault();
        SelectedOutput = Outputs.FirstOrDefault(o => o.Value.ToString() == _settings.KerbSweepOutput) ?? Outputs[0];
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.KerbSweepInterval = (decimal)ToInternal(Interval);
        _settings.KerbSweepVerticalOffset = (decimal)ToInternal(VerticalOffset);
        _settings.KerbSweepFlip = FlipProfile;
        _settings.KerbSweepReview = ReviewBeforeKeeping;
        _settings.KerbSweepCategory = SelectedCategory?.Category.ToString() ?? string.Empty;
        _settings.KerbSweepProfileName = SelectedProfileType?.Name ?? string.Empty;
        _settings.KerbSweepOutput = (SelectedOutput?.Value ?? RoadOutput.DirectShape).ToString();
        _settings.SaveSettings();
    }
}
