using Nice3point.Revit.Extensions;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Grading;

namespace TopoAlign.ViewModels;

internal partial class GradeToSurfaceViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<GradeToSurfaceViewModel>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceIsModelLines))]
    private bool _sourceIsEdges = true;

    [ObservableProperty]
    private string _lineStatus = "No line picked.";

    [ObservableProperty]
    private string _sideStatus = "No side picked.";

    [ObservableProperty]
    private string _toposolidStatus = "No toposolid picked.";

    [ObservableProperty]
    private List<SlopeModeItem> _slopeModes = new();

    [ObservableProperty]
    private SlopeModeItem _selectedSlopeMode = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GradeCommand))]
    private decimal _slopeValue = 3;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GradeCommand))]
    private decimal _maxWidth;

    [ObservableProperty]
    private decimal _verticalOffset;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GradeCommand))]
    private decimal _interval;

    [ObservableProperty]
    private bool _addEdgePoints = true;

    public bool SourceIsModelLines => !SourceIsEdges;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>What the command should ask the user to pick before showing the dialog again.</summary>
    public GradePickKind PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public GradeToSurfaceRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly ForgeTypeId _documentUnit;
    private IReadOnlyList<Curve>? _curves;
    private XYZ? _sidePoint;
    private Toposolid? _toposolid;

    public GradeToSurfaceViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Grade to Surface {informationVersion}";

        SlopeModes = new List<SlopeModeItem>
        {
            new(SlopeMode.Ratio, "Ratio (1 : N)"),
            new(SlopeMode.Percent, "Percent (%)"),
            new(SlopeMode.Degrees, "Degrees (°)")
        };

        _documentUnit = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
        foreach (var unit in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel { DisplayUnit = unit, Label = LabelUtils.GetLabelForUnit(unit) });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == _documentUnit) ?? DisplayUnits.First();
        SelectedSlopeMode = SlopeModes[0];

        LoadSettings();
    }

    private bool CanGrade() =>
        _curves is { Count: > 0 } && _sidePoint is not null && _toposolid is not null
        && SlopeValue > 0 && MaxWidth > 0 && Interval > 0;

    // ---- picking ------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void PickLine() => RequestPick(GradePickKind.Line);

    [RelayCommand]
    private void PickSide() => RequestPick(GradePickKind.Side);

    [RelayCommand]
    private void PickToposolid() => RequestPick(GradePickKind.Toposolid);

    private void RequestPick(GradePickKind kind)
    {
        // Hiding a ShowDialog window ends ShowDialog, so the command does the pick (inside the
        // Revit API context) and then shows this same window again.
        PickRequested = kind;
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs the pick the dialog asked for. Called by the command while the dialog is hidden.</summary>
    internal void RunPick()
    {
        var kind = PickRequested;
        PickRequested = GradePickKind.None;

        try
        {
            switch (kind)
            {
                case GradePickKind.Line:
                    PickLineElements();
                    break;

                case GradePickKind.Side:
                    PickSidePoint();
                    break;

                case GradePickKind.Toposolid:
                    var reference = App.CachedUiApp.ActiveUIDocument.Selection.PickObject(ObjectType.Element, new TopoPickFilter(), "Select the toposolid to grade");
                    SelectToposolid(App.RevitDocument.GetElement(reference));
                    break;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled the pick
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while picking for Grade to Surface");
            TaskDialog.Show("Grade to Surface", ex.Message);
        }
        finally
        {
            GradeCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Chooses the toposolid to grade; false if the element isn't one.</summary>
    internal bool SelectToposolid(Element? element)
    {
        if (element is not Toposolid toposolid)
        {
            return false;
        }

        _toposolid = toposolid;
        ToposolidStatus = string.IsNullOrWhiteSpace(toposolid.Name) ? "Toposolid picked." : toposolid.Name;
        GradeCommand.NotifyCanExecuteChanged();
        return true;
    }

    private void PickLineElements()
    {
        var uidoc = App.CachedUiApp.ActiveUIDocument;
        var curves = new List<Curve>();

        if (SourceIsEdges)
        {
            foreach (var reference in uidoc.Selection.PickObjects(ObjectType.Edge, "Select the edge(s) to grade from"))
            {
                var element = App.RevitDocument.GetElement(reference.ElementId);
                var edge = element.GetGeometryObjectFromReference(reference) as Edge
                           ?? throw new InvalidOperationException("The reference is not an edge.");
                var curve = edge.AsCurve();
                if (element is FamilyInstance instance && JoinGeometryUtils.GetJoinedElements(App.RevitDocument, instance).Count == 0)
                {
                    curve = curve.CreateTransformed(instance.GetTransform());
                }

                curves.Add(curve);
            }
        }
        else
        {
            foreach (var reference in uidoc.Selection.PickObjects(ObjectType.Element, new LinePickFilter(), "Select the model lines to grade from"))
            {
                if (App.RevitDocument.GetElement(reference) is ModelCurve { GeometryCurve: { } curve })
                {
                    curves.Add(curve);
                }
            }
        }

        _curves = null;
        if (curves.Count == 0)
        {
            LineStatus = "No line picked.";
            return;
        }

        var chain = CurveChainer.TryChain(curves, projectToPlan: false);
        if (!chain.IsContinuous)
        {
            LineStatus = chain.ErrorMessage ?? "The curves could not be joined.";
            return;
        }

        _curves = chain.Curves;
        LineStatus = $"{chain.Curves.Count} curve(s), {UnitUtils.ConvertFromInternalUnits(chain.TotalLength, _documentUnit):0.##} {LabelUtils.GetLabelForUnit(_documentUnit)} long.";
    }

    private void PickSidePoint()
    {
        var uidoc = App.CachedUiApp.ActiveUIDocument;
        _sidePoint = uidoc.Selection.PickPoint("Click on the side of the line to grade towards");
        SideStatus = "Side picked.";
    }

    // ---- grading ------------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanGrade))]
    private void Grade()
    {
        if (_curves is null || _sidePoint is null || _toposolid is null)
        {
            return;
        }

        try
        {
            SaveSettings();

            // The surface is edited by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new GradeToSurfaceRequest(
                _toposolid,
                _curves,
                _sidePoint,
                SelectedSlopeMode.Mode,
                (double)SlopeValue,
                ToInternalUnits(MaxWidth),
                ToInternalUnits(VerticalOffset),
                ToInternalUnits(Interval),
                AddEdgePoints);

            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Grade to surface failed");
            TaskDialog.Show("Grade to Surface", ex.Message);
        }
    }

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        MaxWidth = Helpers.UnitConversion.Convert(MaxWidth, oldValue.DisplayUnit, newValue.DisplayUnit);
        VerticalOffset = Helpers.UnitConversion.Convert(VerticalOffset, oldValue.DisplayUnit, newValue.DisplayUnit);
        Interval = Helpers.UnitConversion.Convert(Interval, oldValue.DisplayUnit, newValue.DisplayUnit);
    }

    private double ToInternalUnits(decimal value) => ((double)value).FromUnit(SelectedDisplayUnit.DisplayUnit);

    private decimal FromInternalUnits(decimal value) => decimal.Round(Convert.ToDecimal(((double)value).ToUnit(_documentUnit)), 2);

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        SourceIsEdges = _settings.GradeSource != "ModelLines";
        SelectedSlopeMode = SlopeModes.FirstOrDefault(x => x.Mode.ToString() == _settings.GradeSlopeMode) ?? SlopeModes[0];
        SlopeValue = _settings.GradeSlopeValue;
        MaxWidth = FromInternalUnits(_settings.GradeMaxWidth);
        VerticalOffset = FromInternalUnits(_settings.GradeVerticalOffset);
        Interval = FromInternalUnits(_settings.GradeInterval);
        AddEdgePoints = _settings.GradeAddEdgePoints;
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.GradeSource = SourceIsEdges ? "Edges" : "ModelLines";
        _settings.GradeSlopeMode = SelectedSlopeMode.Mode.ToString();
        _settings.GradeSlopeValue = SlopeValue;
        _settings.GradeMaxWidth = Convert.ToDecimal(ToInternalUnits(MaxWidth));
        _settings.GradeVerticalOffset = Convert.ToDecimal(ToInternalUnits(VerticalOffset));
        _settings.GradeInterval = Convert.ToDecimal(ToInternalUnits(Interval));
        _settings.GradeAddEdgePoints = AddEdgePoints;
        _settings.SaveSettings();
    }
}
#endif