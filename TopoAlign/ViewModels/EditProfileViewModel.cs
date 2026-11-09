using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels;

internal partial class EditProfileViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<EditProfileViewModel>();

    [ObservableProperty]
    private string _alignmentStatus = "No alignment selected.";

    [ObservableProperty]
    private string _profileStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddStationCommand))]
    private decimal _newStation;

    [ObservableProperty]
    private decimal _defaultCurveLength;

    [ObservableProperty]
    private ProfileRow? _selectedRow;

    /// <summary>What the profile graph draws; rebuilt whenever a station, elevation or curve length changes.</summary>
    [ObservableProperty]
    private Views.ProfileGraphData _graph = Views.ProfileGraphData.Empty;

    /// <summary>Position of the selected grid row among the stations, so the graph can highlight it; -1 for none.</summary>
    [ObservableProperty]
    private int _selectedStationIndex = -1;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    /// <summary>The stations of the profile, in order from the start of the alignment.</summary>
    public ObservableCollection<ProfileRow> Rows { get; } = new();

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>True when the dialog was hidden to pick an alignment; the command picks, then shows the dialog again.</summary>
    public bool PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public EditProfileRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private Element? _alignment;
    private string? _originalJson;
    private string _alignmentName = string.Empty;
    private List<ProfileVertex> _original = new();
    private double _startStationFeet;
    private double _profileLength;
    private Transform _sharedToInternal = Transform.Identity;
    private bool _loading;

    public EditProfileViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Edit Profile {informationVersion}";

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

        // elevations in the alignment's JSON are shared elevations; the dialog shows project elevations, as Create Alignment does
        _sharedToInternal = App.RevitDocument.ActiveProjectLocation.GetTotalTransform();

        _settings.LoadSettings();
        DefaultCurveLength = ToDisplay(_settings.EditProfileCurveLength);
    }

    public bool HasAlignment => _alignment is not null;

    private bool CanApply()
    {
        return HasAlignment && TryGetVertices(out var vertices, out _) && !SameAsOriginal(vertices);
    }

    private bool CanAddStation() => HasAlignment;

    partial void OnSelectedRowChanged(ProfileRow? value)
    {
        SelectedStationIndex = value is null ? -1 : Rows.IndexOf(value);
    }

    // ---- units --------------------------------------------------------------------------------------------------

    private double ToInternal(decimal displayValue)
    {
        return ((double)displayValue).FromUnit(SelectedDisplayUnit.DisplayUnit);
    }

    private decimal ToDisplay(decimal internalValue) => ToDisplay((double)internalValue);

    private decimal ToDisplay(double internalValue)
    {
        return decimal.Round((decimal)internalValue.ToUnit(SelectedDisplayUnit.DisplayUnit), 3);
    }

    private double SharedToProject(double sharedElevation) => _sharedToInternal.OfPoint(new XYZ(0.0, 0.0, sharedElevation)).Z;

    private double ProjectToShared(double projectElevation) => _sharedToInternal.Inverse.OfPoint(new XYZ(0.0, 0.0, projectElevation)).Z;

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        // keep the same physical values when the user switches units
        _loading = true;
        try
        {
            foreach (var row in Rows)
            {
                row.Station = ConvertUnits(row.Station, oldValue, newValue);
                row.Elevation = ConvertUnits(row.Elevation, oldValue, newValue);
                row.CurveLength = ConvertUnits(row.CurveLength, oldValue, newValue);
            }

            NewStation = ConvertUnits(NewStation, oldValue, newValue);
            DefaultCurveLength = ConvertUnits(DefaultCurveLength, oldValue, newValue);
        }
        finally
        {
            _loading = false;
        }

        Refresh();
    }

    private static decimal ConvertUnits(decimal value, Models.DisplayUnitModel from, Models.DisplayUnitModel to)
    {
        var feet = UnitUtils.ConvertToInternalUnits((double)value, from.DisplayUnit);
        return decimal.Round((decimal)UnitUtils.ConvertFromInternalUnits(feet, to.DisplayUnit), 3);
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
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), "Select the alignment whose profile to edit");
            SelectAlignment(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting an alignment");
            TaskDialog.Show("Edit Profile", ex.Message);
        }
    }

    /// <summary>Reads an alignment's profile into the grid. False, with the reason in the status, if it can't be edited.</summary>
    internal bool SelectAlignment(Element element)
    {
        _alignment = null;
        _originalJson = null;
        Rows.Clear();
        ProfileStatus = string.Empty;
        Graph = Views.ProfileGraphData.Empty;
        SelectedStationIndex = -1;
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
            if (!document.CanEditProfile(out var reason))
            {
                AlignmentStatus = reason ?? string.Empty;
                return false;
            }

            _original = document.ReadProfile();
            _startStationFeet = document.StartStation;
            _profileLength = _original[_original.Count - 1].Distance;
            _alignmentName = document.Name;
            _originalJson = json;
            _alignment = element;

            LoadRows(_original);
            AlignmentStatus = $"{(string.IsNullOrEmpty(_alignmentName) ? "Alignment" : _alignmentName)}, length {ToDisplay(_profileLength):N2} {SelectedDisplayUnit.Label}";
            NewStation = ToDisplay(_startStationFeet + _profileLength / 2.0);
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
            AddStationCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    private void LoadRows(IReadOnlyList<ProfileVertex> vertices)
    {
        _loading = true;
        try
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowChanged;
            }

            Rows.Clear();
            foreach (var vertex in vertices)
            {
                var row = new ProfileRow(vertex)
                {
                    Station = ToDisplay(_startStationFeet + vertex.Distance),
                    Elevation = ToDisplay(SharedToProject(vertex.Elevation)),
                    CurveLength = ToDisplay(vertex.CurveLength)
                };
                row.PropertyChanged += OnRowChanged;
                Rows.Add(row);
            }
        }
        finally
        {
            _loading = false;
        }

        Refresh();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading || sender is not ProfileRow row)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ProfileRow.Station):
                row.StationEdited = true;
                break;

            case nameof(ProfileRow.Elevation):
                row.ElevationEdited = true;
                break;

            case nameof(ProfileRow.CurveLength):
                row.CurveLengthEdited = true;
                break;

            default:
                return;
        }

        Refresh();
    }

    // ---- turning the grid into a profile ------------------------------------------------------------------------

    /// <summary>The grid as profile points in feet, distances along the alignment and shared elevations. False, with the reason, if it isn't a valid profile.</summary>
    private bool TryGetVertices(out List<ProfileVertex> vertices, out string? error)
    {
        vertices = new List<ProfileVertex>(Rows.Count);
        error = null;

        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            var source = row.Source;
            var isEnd = i == 0 || i == Rows.Count - 1;

            double distance;
            if (source is { } s && (isEnd || !row.StationEdited))
            {
                distance = s.Distance;
            }
            else
            {
                distance = ToInternal(row.Station) - _startStationFeet;
            }

            var elevation = source is { } e && !row.ElevationEdited ? e.Elevation : ProjectToShared(ToInternal(row.Elevation));
            var curve = isEnd ? 0.0 : source is { } c && !row.CurveLengthEdited ? c.CurveLength : ToInternal(row.CurveLength);

            vertices.Add(new ProfileVertex(distance, elevation, curve));
        }

        // an end row that was added or lost keeps the ends of the alignment: only the original ends may be the ends
        if (Rows.Count >= 2 && (Rows[0].Source is null || Rows[Rows.Count - 1].Source is null))
        {
            error = "The first and last stations must stay at the ends of the alignment.";
            return false;
        }

        var errors = ProfileEditing.Validate(vertices, _profileLength);
        if (errors.Count > 0)
        {
            error = errors[0];
            return false;
        }

        return true;
    }

    private bool SameAsOriginal(IReadOnlyList<ProfileVertex> vertices)
    {
        if (vertices.Count != _original.Count)
        {
            return false;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            if (Math.Abs(vertices[i].Distance - _original[i].Distance) > 1e-6 ||
                Math.Abs(vertices[i].Elevation - _original[i].Elevation) > 1e-6 ||
                Math.Abs(vertices[i].CurveLength - _original[i].CurveLength) > 1e-6)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Renumbers the rows, refreshes the grades and the status line, and updates the Apply button.</summary>
    private void Refresh()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].Number = i + 1;
            Rows[i].IsEnd = i == 0 || i == Rows.Count - 1;
        }

        SelectedStationIndex = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);

        if (SelectedDisplayUnit is null || Rows.Count == 0)
        {
            Graph = Views.ProfileGraphData.Empty;
            ApplyCommand.NotifyCanExecuteChanged();
            return;
        }

        if (!TryGetVertices(out var vertices, out var error))
        {
            foreach (var row in Rows)
            {
                row.GradeToNext = string.Empty;
                row.CurveUsed = string.Empty;
            }

            ProfileStatus = error ?? string.Empty;

            // an invalid grid has no profile to draw: keep the original so the user still sees where they started
            Graph = BuildGraph(null);
        }
        else
        {
            for (var i = 0; i < Rows.Count; i++)
            {
                Rows[i].GradeToNext = i < Rows.Count - 1 ? ProfileEditing.FormatGrade(ProfileEditing.Grade(vertices[i], vertices[i + 1])) :string.Empty;

                var used = ProfileEditing.EffectiveCurveLength(vertices, i);
                Rows[i].CurveUsed = used > 1e-9 ? ToDisplay(used).ToString("0.###") : string.Empty;
            }

            var minElevation = ToDisplay(SharedToProject(vertices.Min(v => v.Elevation)));
            var maxElevation = ToDisplay(SharedToProject(vertices.Max(v => v.Elevation)));
            ProfileStatus = $"{vertices.Count} stations, elevation {minElevation:N2} to {maxElevation:N2} {SelectedDisplayUnit.Label}, " +
                            $"max grade {ProfileEditing.MaxAbsGrade(vertices):P1}";

            Graph = BuildGraph(vertices);
        }

        ApplyCommand.NotifyCanExecuteChanged();
    }

    /// <summary>The graph's data in display units. <paramref name="edited"/> is null when the grid isn't a valid profile.</summary>
    private Views.ProfileGraphData BuildGraph(IReadOnlyList<ProfileVertex>? edited)
    {
        System.Windows.Point ToPoint(double distance, double sharedElevation) => new(
            UnitUtils.ConvertFromInternalUnits(_startStationFeet + distance, SelectedDisplayUnit.DisplayUnit),
            UnitUtils.ConvertFromInternalUnits(SharedToProject(sharedElevation), SelectedDisplayUnit.DisplayUnit));

        List<System.Windows.Point> Line(IReadOnlyList<ProfileVertex> vertices) =>
            ProfileSampling.Sample(vertices).Select(p => ToPoint(p.Distance, p.Elevation)).ToList();

        return new Views.ProfileGraphData(
            edited is null ? new List<System.Windows.Point>() : Line(edited),
            Line(_original),
            edited?.Select(v => ToPoint(v.Distance, v.Elevation)).ToList() ?? new List<System.Windows.Point>(),
            SelectedDisplayUnit.Label);
    }

    // ---- editing the stations -----------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanAddStation))]
    private void AddStation()
    {
        if (!TryGetVertices(out var vertices, out var error))
        {
            ProfileStatus = "Fix the stations first: " + error;
            return;
        }

        try
        {
            var distance = ToInternal(NewStation) - _startStationFeet;
            var curveLength = Math.Max(ToInternal(DefaultCurveLength), 0.0);
            var inserted = ProfileEditing.Insert(vertices, distance, curveLength);
            var index = inserted.FindIndex(v => Math.Abs(v.Distance - distance) < ProfileEditing.MinSpacing);

            var row = new ProfileRow(null);
            _loading = true;
            try
            {
                row.Station = NewStation;
                row.Elevation = ToDisplay(SharedToProject(inserted[index].Elevation));
                row.CurveLength = DefaultCurveLength;
            }
            finally
            {
                _loading = false;
            }

            row.StationEdited = row.ElevationEdited = row.CurveLengthEdited = true;
            row.PropertyChanged += OnRowChanged;
            Rows.Insert(index, row);
            SelectedRow = row;
            Refresh();
        }
        catch (InvalidOperationException ex)
        {
            ProfileStatus = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteStation()
    {
        var row = SelectedRow;
        if (row is null || row.IsEnd)
        {
            ProfileStatus = "Select a station between the first and the last to remove it.";
            return;
        }

        row.PropertyChanged -= OnRowChanged;
        Rows.Remove(row);
        SelectedRow = null;
        Refresh();
    }

    [RelayCommand]
    private void ResetStations()
    {
        if (HasAlignment)
        {
            LoadRows(_original);
        }
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
            if (!TryGetVertices(out var vertices, out var error))
            {
                ProfileStatus = error ?? string.Empty;
                return;
            }

            var document = AlignmentJsonDocument.Parse(_originalJson);
            document.SetProfile(vertices);

            var kept = Rows.Where(r => r.Source is not null).ToList();
            var added = Rows.Count - kept.Count;
            var removed = _original.Count - kept.Count;
            var changed = 0;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Source is not { } source)
                {
                    continue;
                }

                if (Math.Abs(vertices[i].Distance - source.Distance) > 1e-6 ||
                    Math.Abs(vertices[i].Elevation - source.Elevation) > 1e-6 ||
                    Math.Abs(vertices[i].CurveLength - source.CurveLength) > 1e-6)
                {
                    changed++;
                }
            }

            _settings.LoadSettings();
            _settings.EditProfileCurveLength = (decimal)ToInternal(DefaultCurveLength);
            _settings.SaveSettings();

            // The alignment is updated by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new EditProfileRequest(_alignment, _alignmentName, document.ToJson(), _profileLength, added, removed, changed);
            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Edit profile failed");
            TaskDialog.Show("Edit Profile", ex.Message);
        }
    }
}