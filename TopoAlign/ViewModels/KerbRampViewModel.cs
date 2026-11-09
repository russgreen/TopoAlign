using Nice3point.Revit.Extensions;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Grading;

namespace TopoAlign.ViewModels;

internal partial class KerbRampViewModel : BaseViewModel
{
    private static readonly ILogger log = Log.ForContext<KerbRampViewModel>();

    [ObservableProperty]
    private string _kerbStatus = "No kerb edge picked.";

    [ObservableProperty]
    private string _centreStatus = "No centre point picked.";

    [ObservableProperty]
    private string _targetStatus = "No target edge picked.";

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit = null!;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _width;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _rampGradient = 12;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _flareGradient = 12;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private decimal _maxRampLength;

    [ObservableProperty]
    private decimal _levelOffset;

    public string WindowTitle { get; }

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    /// <summary>What the command should ask the user to pick before showing the dialog again.</summary>
    public KerbPickKind PickRequested { get; set; }

    /// <summary>Set when the user confirms; consumed by the command after the dialog closes.</summary>
    public KerbRampRequest? Request { get; private set; }

    private readonly Models.Settings _settings = new();
    private readonly ForgeTypeId _documentUnit;
    private Toposolid? _kerbToposolid;
    private XYZ? _kerbAnchor;
    private Toposolid? _targetToposolid;
    private XYZ? _targetAnchor;
    private XYZ? _centre;

    public KerbRampViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        WindowTitle = $"Dropped Kerb Ramp {informationVersion}";

        _documentUnit = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
        foreach (var unit in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel { DisplayUnit = unit, Label = LabelUtils.GetLabelForUnit(unit) });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == _documentUnit) ?? DisplayUnits.First();

        LoadSettings();
    }

    private bool CanCreate() =>
        _kerbToposolid is not null && _kerbAnchor is not null
        && _targetToposolid is not null && _targetAnchor is not null
        && _centre is not null
        && Width > 0 && RampGradient > 0 && FlareGradient > 0 && MaxRampLength > 0;

    // ---- picking ------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void PickKerb() => RequestPick(KerbPickKind.Kerb);

    [RelayCommand]
    private void PickCentre() => RequestPick(KerbPickKind.Centre);

    [RelayCommand]
    private void PickTarget() => RequestPick(KerbPickKind.Target);

    private void RequestPick(KerbPickKind kind)
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
        PickRequested = KerbPickKind.None;

        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            switch (kind)
            {
                case KerbPickKind.Kerb:
                    (_kerbToposolid, _kerbAnchor) = PickToposolidEdge("Select the edge of the footway toposolid at the kerb");
                    KerbStatus = DescribeToposolid(_kerbToposolid);
                    break;

                case KerbPickKind.Centre:
                    _centre = PickCentre(uidoc);
                    CentreStatus = "Centre point picked.";
                    break;

                case KerbPickKind.Target:
                    (_targetToposolid, _targetAnchor) = PickToposolidEdge("Select the edge to drop down to, usually the edge of the road toposolid");
                    TargetStatus = DescribeToposolid(_targetToposolid);
                    break;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled the pick
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while picking for Dropped Kerb Ramp");
            TaskDialog.Show("Dropped Kerb Ramp", ex.Message);
        }
        finally
        {
            CreateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// The centre of the drop. A plan view can be clicked anywhere; other views, such as 3D, have no work plane to click in,
    /// so there the point is picked on the toposolid itself.
    /// </summary>
    private static XYZ PickCentre(UIDocument uidoc)
    {
        if (App.RevitDocument.ActiveView is ViewPlan)
        {
            return uidoc.Selection.PickPoint("Click the centre of the dropped kerb, close to the kerb edge");
        }

        var reference = uidoc.Selection.PickObject(ObjectType.PointOnElement, new TopoEdgePickFilter(), "Click on the footway toposolid at the centre of the dropped kerb, close to the kerb edge");
        return reference.GlobalPoint ?? throw new InvalidOperationException("No point could be read from that pick.");
    }

    private static string DescribeToposolid(Toposolid? toposolid) =>
        toposolid is null || string.IsNullOrWhiteSpace(toposolid.Name) ? "Edge picked." : $"Edge of {toposolid.Name}.";

    /// <summary>Asks for an edge of a toposolid, and returns the toposolid with a point on the edge.</summary>
    private static (Toposolid Toposolid, XYZ Anchor) PickToposolidEdge(string prompt)
    {
        var uidoc = App.CachedUiApp.ActiveUIDocument;
        var reference = uidoc.Selection.PickObject(ObjectType.Edge, new TopoEdgePickFilter(), prompt);

        var element = App.RevitDocument.GetElement(reference.ElementId);
        if (element is not Toposolid toposolid)
        {
            throw new InvalidOperationException("The edge is not on a toposolid.");
        }

        var edge = element.GetGeometryObjectFromReference(reference) as Edge
                   ?? throw new InvalidOperationException("The reference is not an edge.");
        var curve = edge.AsCurve();
        var start = curve.GetEndPoint(0);
        var end = curve.GetEndPoint(1);
        if (Math.Abs(end.X - start.X) < 1e-6 && Math.Abs(end.Y - start.Y) < 1e-6)
        {
            throw new InvalidOperationException("That edge is vertical. Pick an edge along the top of the toposolid.");
        }

        return (toposolid, curve.Evaluate(0.5, true));
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (_kerbToposolid is null || _kerbAnchor is null || _targetToposolid is null || _targetAnchor is null || _centre is null)
        {
            return;
        }

        try
        {
            SaveSettings();

            // The surface is edited by the command once this dialog has closed, so the transaction
            // runs inside the Revit API context rather than from a WPF button handler.
            Request = new KerbRampRequest(
                _kerbToposolid,
                _kerbAnchor,
                _targetToposolid,
                _targetAnchor,
                _centre,
                ToInternalUnits(Width),
                (double)RampGradient,
                (double)FlareGradient,
                ToInternalUnits(MaxRampLength),
                ToInternalUnits(LevelOffset));

            OnClosingRequest();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Dropped kerb ramp failed");
            TaskDialog.Show("Dropped Kerb Ramp", ex.Message);
        }
    }

    partial void OnSelectedDisplayUnitChanged(Models.DisplayUnitModel? oldValue, Models.DisplayUnitModel newValue)
    {
        if (oldValue is null || newValue is null)
        {
            return;
        }

        Width = Helpers.UnitConversion.Convert(Width, oldValue.DisplayUnit, newValue.DisplayUnit);
        MaxRampLength = Helpers.UnitConversion.Convert(MaxRampLength, oldValue.DisplayUnit, newValue.DisplayUnit);
        LevelOffset = Helpers.UnitConversion.Convert(LevelOffset, oldValue.DisplayUnit, newValue.DisplayUnit);
    }

    private double ToInternalUnits(decimal value) => ((double)value).FromUnit(SelectedDisplayUnit.DisplayUnit);

    private decimal FromInternalUnits(decimal value) => decimal.Round(Convert.ToDecimal(((double)value).ToUnit(_documentUnit)), 2);

    // ---- settings -----------------------------------------------------------------------------------------------

    private void LoadSettings()
    {
        _settings.LoadSettings();

        Width = FromInternalUnits(_settings.KerbRampWidth);
        RampGradient = _settings.KerbRampGradient;
        FlareGradient = _settings.KerbFlareGradient;
        MaxRampLength = FromInternalUnits(_settings.KerbRampMaxLength);
        LevelOffset = FromInternalUnits(_settings.KerbRampLevelOffset);
    }

    private void SaveSettings()
    {
        _settings.LoadSettings();
        _settings.KerbRampWidth = Convert.ToDecimal(ToInternalUnits(Width));
        _settings.KerbRampGradient = RampGradient;
        _settings.KerbFlareGradient = FlareGradient;
        _settings.KerbRampMaxLength = Convert.ToDecimal(ToInternalUnits(MaxRampLength));
        _settings.KerbRampLevelOffset = Convert.ToDecimal(ToInternalUnits(LevelOffset));
        _settings.SaveSettings();
    }
}
#endif