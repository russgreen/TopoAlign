using static TopoAlign.Tests.TestKit;
using Settings = TopoAlign.Models.Settings;
using Autodesk.Revit.DB;
using System.IO;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using TopoAlign.Models;
using TopoAlign.ViewModels;

namespace TopoAlign.Tests;

public class ProjectPhasesViewModelTests : TopoAlignTest
{
    [Test]
    public async Task SelectsTheFirstTwoPhasesByDefault()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "Existing", "New Construction", "Demolished" });

        await Assert.That(viewModel.Phases.Count).IsEqualTo(3);
        await Assert.That(viewModel.SourcePhase).IsEqualTo("Existing");
        await Assert.That(viewModel.TargetPhase).IsEqualTo("New Construction");
        await Assert.That(viewModel.WindowTitle).IsEqualTo("Project Phases");
    }

    [Test]
    public async Task SinglePhase_IsUsedAsBothSourceAndTarget()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "Only" });

        await Assert.That(viewModel.SourcePhase).IsEqualTo("Only");
        await Assert.That(viewModel.TargetPhase).IsEqualTo("Only");
    }

    [Test]
    public async Task NoPhases_DisablesOk()
    {
        var viewModel = new ProjectPhasesViewModel(Array.Empty<string>());

        await Assert.That(viewModel.SourcePhase).IsNull();
        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Ok_RaisesClosingRequest()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "A", "B" });
        var raised = 0;
        viewModel.ClosingRequest += (_, _) => raised++;

        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsTrue();
        viewModel.OkCommand.Execute(null);

        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task ClearingASelection_DisablesOkAndNotifiesTheCommand()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "A", "B" });
        var canExecuteChanged = 0;
        viewModel.OkCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        viewModel.TargetPhase = null;

        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsFalse();
        await Assert.That(canExecuteChanged).IsGreaterThan(0);
    }

    [Test]
    public async Task Ok_WithoutClosingRequestSubscribers_DoesNotThrow()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "A", "B" });

        var exception = Catch(() => viewModel.OkCommand.Execute(null));

        await Assert.That(exception).IsNull();
    }
}

public class ResetTopoViewModelTests : TopoAlignTest
{
    private static List<TopoItemModel> Items(params string[] labels)
    {
        return labels.Select((label, index) => new TopoItemModel { Id = index + 1, Label = label }).ToList();
    }

    [Test]
    public async Task SelectsTheFirstSourceAndTarget()
    {
        var viewModel = new ResetTopoViewModel(Items("S1", "S2"), Items("T1", "T2", "T3"));

        await Assert.That(viewModel.SourceTopos.Count).IsEqualTo(2);
        await Assert.That(viewModel.TargetTopos.Count).IsEqualTo(3);
        await Assert.That(viewModel.SelectedSource.Label).IsEqualTo("S1");
        await Assert.That(viewModel.SelectedTarget.Label).IsEqualTo("T1");
        await Assert.That(viewModel.WindowTitle).IsEqualTo("Reset Topo");
    }

    [Test]
    public async Task NoTargets_DisablesOk()
    {
        var viewModel = new ResetTopoViewModel(Items("S1"), new List<TopoItemModel>());

        await Assert.That(viewModel.SelectedTarget).IsNull();
        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Ok_RaisesClosingRequestWhenBothAreSelected()
    {
        var viewModel = new ResetTopoViewModel(Items("S1"), Items("T1"));
        var raised = 0;
        viewModel.ClosingRequest += (_, _) => raised++;

        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsTrue();
        viewModel.OkCommand.Execute(null);

        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task ChangingTheSelection_ReEvaluatesOk()
    {
        var viewModel = new ResetTopoViewModel(Items("S1", "S2"), Items("T1"));
        var changed = 0;
        viewModel.OkCommand.CanExecuteChanged += (_, _) => changed++;

        viewModel.SelectedSource = null;

        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsFalse();
        await Assert.That(changed).IsGreaterThan(0);

        viewModel.SelectedSource = viewModel.SourceTopos[1];
        await Assert.That(viewModel.OkCommand.CanExecute(null)).IsTrue();
    }
}

public class DivideLinesViewModelTests : TopoAlignTest
{
    private const decimal FiveMetres = 16.4041994750656m;

    [Test]
    public async Task Constructor_PopulatesUnitsAndConvertsTheDivideDistance()
    {
        var document = NewDocument(UnitSystem.Metric);

        var viewModel = new DivideLinesViewModel(document, FiveMetres);

        await Assert.That(viewModel.DisplayUnits.Count).IsGreaterThan(0);
        await Assert.That(viewModel.SelectedDisplayUnit).IsNotNull();
        await Assert.That(viewModel.Offset).IsEqualTo(0m);
        var expected = decimal.Round(Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)FiveMetres, viewModel.SelectedDisplayUnit.DisplayUnit)), 2);
        await Assert.That(viewModel.Divide).IsEqualTo(expected);
    }

    [Test]
    public async Task Constructor_StoresTheFlags()
    {
        var document = NewDocument();

        var defaults = new DivideLinesViewModel(document, 1m);
        var custom = new DivideLinesViewModel(document, 1m, isDivideEnabled: false, allowNegativeOffset: true);

        await Assert.That(defaults.IsDivideEnabled).IsTrue();
        await Assert.That(defaults.AllowNegativeOffset).IsFalse();
        await Assert.That(custom.IsDivideEnabled).IsFalse();
        await Assert.That(custom.AllowNegativeOffset).IsTrue();
        await Assert.That(defaults.WindowTitle).IsEqualTo("Divide Lines");
    }

    [Test]
    public async Task InternalValues_RoundTripThroughTheSelectedUnit()
    {
        var document = NewDocument(UnitSystem.Metric);
        var viewModel = new DivideLinesViewModel(document, FiveMetres);
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.Single(u => u.DisplayUnit == UnitTypeId.Meters);

        viewModel.Divide = 2m;
        viewModel.Offset = -0.5m;

        await Assert.That((double)viewModel.InternalDivide).IsEqualTo(2 / 0.3048).Within(1e-6);
        await Assert.That((double)viewModel.InternalOffset).IsEqualTo(-0.5 / 0.3048).Within(1e-6);
    }

    [Test]
    public async Task ChangingTheUnit_ChangesTheInternalValue()
    {
        var document = NewDocument(UnitSystem.Metric);
        var viewModel = new DivideLinesViewModel(document, 1m);
        viewModel.Divide = 1000m;

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.Single(u => u.DisplayUnit == UnitTypeId.Millimeters);
        var millimetres = viewModel.InternalDivide;
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.Single(u => u.DisplayUnit == UnitTypeId.Meters);
        var metres = viewModel.InternalDivide;

        await Assert.That((double)millimetres).IsEqualTo(1000 / 304.8).Within(1e-6);
        await Assert.That((double)metres).IsEqualTo(1000 / 0.3048).Within(1e-3);
    }

    [Test]
    public async Task Ok_RaisesClosingRequest()
    {
        var viewModel = new DivideLinesViewModel(NewDocument(), 1m);
        var raised = 0;
        viewModel.ClosingRequest += (_, _) => raised++;

        viewModel.OkCommand.Execute(null);

        await Assert.That(raised).IsEqualTo(1);
    }
}

public class AlignTopoViewModelTests : TopoAlignUiTest
{
    private Document _previousDocument;

    [Before(Test)]
    public void RememberAppDocument()
    {
        _previousDocument = App.RevitDocument;
    }

    [After(Test)]
    public void RestoreAppDocument()
    {
        App.RevitDocument = _previousDocument;
    }

    [Test]
    public async Task Constructor_UsesDefaultsWhenNothingIsSaved()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();

        var viewModel = new AlignTopoViewModel();

        await Assert.That(viewModel.SingleElement).IsTrue();
        await Assert.That(viewModel.TopFace).IsTrue();
        await Assert.That(viewModel.CleanTopoPoints).IsTrue();
        await Assert.That(viewModel.Edge).IsFalse();
        await Assert.That(viewModel.BottomFace).IsFalse();
        await Assert.That(viewModel.WindowTitle.StartsWith("TopoAlign ")).IsTrue();
        await Assert.That(viewModel.DisplayUnits.Count).IsGreaterThan(0);
        await Assert.That(viewModel.SelectedDisplayUnit).IsNotNull();
    }

    [Test]
    public async Task Constructor_LoadsSavedSettings()
    {
        using var backup = new RegistryBackup();
        new Settings
        {
            SingleElement = false,
            TopFace = false,
            CleanTopoPoints = false,
            DivideEdgeDistance = 10m,
            VerticalOffset = 2m
        }.SaveSettings();
        App.RevitDocument = NewDocument();

        var viewModel = new AlignTopoViewModel();

        await Assert.That(viewModel.SingleElement).IsFalse();
        await Assert.That(viewModel.Edge).IsTrue();
        await Assert.That(viewModel.TopFace).IsFalse();
        await Assert.That(viewModel.BottomFace).IsTrue();
        await Assert.That(viewModel.CleanTopoPoints).IsFalse();
        var expectedDivide = decimal.Round(Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits(10, viewModel.SelectedDisplayUnit.DisplayUnit)), 2);
        await Assert.That(viewModel.Divide).IsEqualTo(expectedDivide);
    }

    [Test]
    public async Task ChangingSingleElementAndTopFace_NotifiesTheDerivedProperties()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new AlignTopoViewModel();
        var changed = new List<string>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SingleElement = false;
        viewModel.TopFace = false;

        await Assert.That(changed.Contains("Edge")).IsTrue();
        await Assert.That(changed.Contains("BottomFace")).IsTrue();
        await Assert.That(viewModel.Edge).IsTrue();
        await Assert.That(viewModel.BottomFace).IsTrue();
    }
}

public class CreateAlignmentViewModelTests : TopoAlignUiTest
{
    private Document _previousDocument;

    [Before(Test)]
    public void RememberAppDocument()
    {
        _previousDocument = App.RevitDocument;
    }

    [After(Test)]
    public void RestoreAppDocument()
    {
        App.RevitDocument = _previousDocument;
    }

    private CreateAlignmentViewModel NewViewModel(Document document = null)
    {
        App.RevitDocument = document ?? NewDocument();
        return new CreateAlignmentViewModel();
    }

    /// <summary>Puts the view model into the state it reaches after a successful pick (the pick itself needs Revit's UI).</summary>
    private static void SetChain(CreateAlignmentViewModel viewModel, IEnumerable<Curve> curves, IEnumerable<ElementId> ids = null)
    {
        var list = curves.ToList();
        var type = typeof(CreateAlignmentViewModel);
        type.GetField("_orderedCurves", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, list);
        if (ids is not null)
        {
            ((List<ElementId>)type.GetField("_selectedCurveIds", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel)!).AddRange(ids);
        }

        viewModel.SelectedCurveCount = list.Count;
        viewModel.IsChainContinuous = true;
    }

    private static readonly Curve[] TwoLines = { Ln(0, 0, 100, 0), Ln(100, 0, 100, 50) };

    // ---- construction -------------------------------------------------------------------------------------------

    [Test]
    public async Task Constructor_StartsEmptyWithDefaults()
    {
        using var backup = new RegistryBackup();

        var viewModel = NewViewModel();

        await Assert.That(viewModel.AlignmentName).IsEqualTo("Alignment 1");
        await Assert.That(viewModel.StartStation).IsEqualTo(0m);
        await Assert.That(viewModel.ReverseDirection).IsFalse();
        await Assert.That(viewModel.DeleteSourceLines).IsFalse();
        await Assert.That(viewModel.ChainStatus).IsEqualTo("No lines selected.");
        await Assert.That(viewModel.IsChainContinuous).IsFalse();
        await Assert.That(viewModel.SelectedCurveCount).IsEqualTo(0);
        await Assert.That(viewModel.WindowTitle.StartsWith("Create Alignment ")).IsTrue();
        await Assert.That(viewModel.DisplayUnits.Count).IsGreaterThan(0);
        await Assert.That(viewModel.SelectedDisplayUnit).IsNotNull();
        await Assert.That(viewModel.Request).IsNull();
        await Assert.That(viewModel.PickRequested).IsFalse();
    }

    [Test]
    public async Task Constructor_LoadsSavedOptions()
    {
        using var backup = new RegistryBackup();
        new Settings { CreateAlignmentReverseDirection = true, CreateAlignmentDeleteSourceLines = true }.SaveSettings();

        var viewModel = NewViewModel();

        await Assert.That(viewModel.ReverseDirection).IsTrue();
        await Assert.That(viewModel.DeleteSourceLines).IsTrue();
    }

    [Test]
    public async Task Constructor_DefaultNameSkipsNamesAlreadyInTheModel()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var json = TopoAlign.Alignments.AlignmentJsonBuilder.Build("Alignment 1", TwoLines, internalToShared, 0.0, out var length);
        TopoAlign.Alignments.AlignmentApi.Create(document, "Alignment 1", json, length);

        var viewModel = NewViewModel(document);

        await Assert.That(viewModel.AlignmentName).IsEqualTo("Alignment 2");
    }

    // ---- commands -----------------------------------------------------------------------------------------------

    [Test]
    public async Task CreateAlignment_IsDisabledUntilLinesAreChained()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();

        SetChain(viewModel, TwoLines);
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task CreateAlignment_IsDisabledForABlankName()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, TwoLines);

        viewModel.AlignmentName = "   ";

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();

        viewModel.AlignmentName = "Road";
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task PickLines_RequestsAPickAndAsksTheWindowToHide()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickLinesCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task CreateAlignment_BuildsTheRequestAndClosesTheDialog()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        viewModel.AlignmentName = "Access Road";
        SetChain(viewModel, TwoLines);
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.CreateAlignmentCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Name).IsEqualTo("Access Road");
        await Assert.That(request.PlanLength).IsEqualTo(150.0).Within(1e-6);
        await Assert.That(request.SourceCurveIdsToDelete).IsNull();
        var curves = Curves(HorizontalGeometry(Parse(request.Json)));
        await Assert.That(curves.Count).IsEqualTo(2);
        await Assert.That(CoreComponent(Parse(request.Json))["String"]!["name"]!.GetValue<string>()).IsEqualTo("Access Road");
    }

    [Test]
    public async Task CreateAlignment_ReverseDirection_StartsFromTheEndOfTheChain()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, TwoLines);
        viewModel.ReverseDirection = true;

        viewModel.CreateAlignmentCommand.Execute(null);

        var first = Curves(HorizontalGeometry(Parse(viewModel.Request.Json)))[0]!;
        var position = PointOf(first, Point2DKey, "position");
        await Assert.That(position.X).IsEqualTo(100.0).Within(1e-6);
        await Assert.That(position.Y).IsEqualTo(50.0).Within(1e-6);
    }

    [Test]
    public async Task CreateAlignment_StartStation_IsConvertedFromTheDisplayUnitToFeet()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, TwoLines);
        viewModel.StartStation = 250m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var expected = UnitUtils.ConvertToInternalUnits(250, viewModel.SelectedDisplayUnit.DisplayUnit);
        var startStation = CoreComponent(Parse(viewModel.Request.Json))["Float64"]!["startstation"]!.GetValue<double>();
        await Assert.That(startStation).IsEqualTo(expected).Within(1e-6);
    }

    [Test]
    public async Task CreateAlignment_DeleteSourceLines_PassesTheSelectedElementIds()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var first = NewModelCurve(document, TwoLines[0]);
        var second = NewModelCurve(document, TwoLines[1]);
        var viewModel = NewViewModel(document);
        SetChain(viewModel, TwoLines, new[] { first.Id, second.Id });
        viewModel.DeleteSourceLines = true;

        viewModel.CreateAlignmentCommand.Execute(null);

        var ids = viewModel.Request.SourceCurveIdsToDelete;
        await Assert.That(ids.Count).IsEqualTo(2);
        await Assert.That(ids.Contains(first.Id)).IsTrue();
        await Assert.That(ids.Contains(second.Id)).IsTrue();
    }

    [Test]
    public async Task CreateAlignment_RemembersTheOptionsForNextTime()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, TwoLines);
        viewModel.ReverseDirection = true;
        viewModel.DeleteSourceLines = true;
        viewModel.StartStation = 100m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var saved = new Settings();
        saved.LoadSettings();
        await Assert.That(saved.CreateAlignmentReverseDirection).IsTrue();
        await Assert.That(saved.CreateAlignmentDeleteSourceLines).IsTrue();
        var expected = UnitUtils.ConvertToInternalUnits(100, viewModel.SelectedDisplayUnit.DisplayUnit);
        await Assert.That((double)saved.CreateAlignmentStartStation).IsEqualTo(expected).Within(1e-6);
    }

    // ---- status text --------------------------------------------------------------------------------------------

    [Test]
    public async Task ChangingTheDisplayUnit_RefreshesTheChainStatus()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, TwoLines);
        var feet = viewModel.DisplayUnits.Single(u => u.DisplayUnit == UnitTypeId.Feet);
        var metres = viewModel.DisplayUnits.Single(u => u.DisplayUnit == UnitTypeId.Meters);

        viewModel.SelectedDisplayUnit = feet;
        var inFeet = viewModel.ChainStatus;
        viewModel.SelectedDisplayUnit = metres;
        var inMetres = viewModel.ChainStatus;

        await Assert.That(inFeet.StartsWith("2 lines, continuous, ")).IsTrue();
        await Assert.That(inFeet.EndsWith(feet.Label)).IsTrue();
        await Assert.That(inMetres.EndsWith(metres.Label)).IsTrue();
        await Assert.That(inFeet).IsNotEqualTo(inMetres);
    }

    [Test]
    public async Task ChangingTheDisplayUnit_WithoutAChain_LeavesTheStatusAlone()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u != viewModel.SelectedDisplayUnit);

        await Assert.That(viewModel.ChainStatus).IsEqualTo("No lines selected.");
    }
}
