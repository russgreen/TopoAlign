using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Reflection;
using System.Windows;
using Settings = TopoAlign.Models.Settings;
using Visibility = System.Windows.Visibility;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Tests;

/// <summary>Drape-on-toposolid options of the Create Alignment dialog.</summary>
public class CreateAlignmentDrapeTests : TopoAlignUiTest
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

    private static void SetChain(CreateAlignmentViewModel viewModel, IEnumerable<Curve> curves)
    {
        var list = curves.ToList();
        typeof(CreateAlignmentViewModel).GetField("_orderedCurves", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, list);
        viewModel.SelectedCurveCount = list.Count;
        viewModel.IsChainContinuous = true;
    }

    /// <summary>Stands in for a picked toposolid (the pick needs Revit's UI): any element with upward-facing geometry works.</summary>
    private static void SetTopography(CreateAlignmentViewModel viewModel, Element element)
    {
        typeof(CreateAlignmentViewModel).GetField("_topography", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, element);
        viewModel.CreateAlignmentCommand.NotifyCanExecuteChanged();
    }

    private static DirectShape BoxTopography(Document document, double width, double depth, double height)
    {
        using var transaction = new Transaction(document, "Create ground");
        transaction.Start();
        var shape = DirectShape.CreateElement(document, new ElementId(BuiltInCategory.OST_GenericModel));
        shape.SetShape(new List<GeometryObject> { Box(width, depth, height) });
        transaction.Commit();
        return shape;
    }

    private static void UseFeet(CreateAlignmentViewModel viewModel)
    {
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
    }

    // ---- defaults and settings ---------------------------------------------------------------------------------

    [Test]
    public async Task Constructor_DrapeIsOffWithSensibleDefaults()
    {
        using var backup = new RegistryBackup();

        var viewModel = NewViewModel();

        await Assert.That(viewModel.DrapeOnTopo).IsFalse();
        await Assert.That(viewModel.SampleInterval).IsGreaterThan(0m);
        await Assert.That(viewModel.VerticalTolerance).IsGreaterThan(0m);
        await Assert.That(viewModel.ElevationOffset).IsEqualTo(0m);
        await Assert.That(viewModel.MinVerticalCurveLength).IsEqualTo(0m);
        await Assert.That(viewModel.TopoStatus).IsEqualTo("No toposolid selected.");
        await Assert.That(viewModel.ProfilePreview).IsEqualTo(string.Empty);
        await Assert.That(viewModel.PickToposolidRequested).IsFalse();
    }

    [Test]
    public async Task Settings_DefaultToOneMetreSamplingAndFiftyMillimetreTolerance()
    {
        using var backup = new RegistryBackup();

        var settings = new Settings();

        await Assert.That(settings.CreateAlignmentDrapeOnTopo).IsFalse();
        await Assert.That((double)settings.CreateAlignmentSampleInterval).IsEqualTo(1.0 / 0.3048).Within(1e-9);
        await Assert.That((double)settings.CreateAlignmentVerticalTolerance).IsEqualTo(0.05 / 0.3048).Within(1e-9);
        await Assert.That(settings.CreateAlignmentElevationOffset).IsEqualTo(0m);
        await Assert.That(settings.CreateAlignmentMinVerticalCurveLength).IsEqualTo(0m);
    }

    [Test]
    public async Task Settings_DrapeOptionsRoundTripThroughTheRegistry()
    {
        using var backup = new RegistryBackup();
        new Settings
        {
            CreateAlignmentDrapeOnTopo = true,
            CreateAlignmentSampleInterval = 12.5m,
            CreateAlignmentVerticalTolerance = 0.4m,
            CreateAlignmentElevationOffset = -1.25m,
            CreateAlignmentMinVerticalCurveLength = 60m
        }.SaveSettings();

        var loaded = new Settings();
        loaded.LoadSettings();

        await Assert.That(loaded.CreateAlignmentDrapeOnTopo).IsTrue();
        await Assert.That(loaded.CreateAlignmentSampleInterval).IsEqualTo(12.5m);
        await Assert.That(loaded.CreateAlignmentVerticalTolerance).IsEqualTo(0.4m);
        await Assert.That(loaded.CreateAlignmentElevationOffset).IsEqualTo(-1.25m);
        await Assert.That(loaded.CreateAlignmentMinVerticalCurveLength).IsEqualTo(60m);
    }

    [Test]
    public async Task Constructor_LoadsSavedDrapeOptionsInDisplayUnits()
    {
        using var backup = new RegistryBackup();
        new Settings
        {
            CreateAlignmentDrapeOnTopo = true,
            CreateAlignmentSampleInterval = 10m,
            CreateAlignmentMinVerticalCurveLength = 40m
        }.SaveSettings();

        var viewModel = NewViewModel();

        var unit = viewModel.SelectedDisplayUnit.DisplayUnit;
        await Assert.That(viewModel.DrapeOnTopo).IsTrue();
        await Assert.That((double)viewModel.SampleInterval).IsEqualTo(Math.Round(UnitUtils.ConvertFromInternalUnits(10.0, unit), 3)).Within(1e-9);
        await Assert.That((double)viewModel.MinVerticalCurveLength).IsEqualTo(Math.Round(UnitUtils.ConvertFromInternalUnits(40.0, unit), 3)).Within(1e-9);
    }

    // ---- enabling Create ----------------------------------------------------------------------------------------

    [Test]
    public async Task CreateAlignment_WithDrapeOnButNoToposolid_IsDisabled()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, new Curve[] { Ln(0, 0, 100, 0) });
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();

        viewModel.DrapeOnTopo = true;

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task CreateAlignment_WithDrapeOnAndAToposolid_IsEnabled()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        viewModel.DrapeOnTopo = true;

        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task CreateAlignment_WithDrapeOff_DoesNotNeedAToposolid()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, new Curve[] { Ln(0, 0, 100, 0) });
        viewModel.DrapeOnTopo = false;

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
    }

    // ---- picking ------------------------------------------------------------------------------------------------

    [Test]
    public async Task PickToposolid_RequestsAToposolidPickAndHidesTheWindow()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickToposolidCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(viewModel.PickToposolidRequested).IsTrue();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task PickLines_AfterPickToposolid_RequestsLinesAgain()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        viewModel.PickToposolidCommand.Execute(null);

        viewModel.PickLinesCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(viewModel.PickToposolidRequested).IsFalse();
    }

    // ---- preview ------------------------------------------------------------------------------------------------

    [Test]
    public async Task PreviewProfile_WithoutLines_AsksForLines()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        viewModel.DrapeOnTopo = true;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview).IsEqualTo("Pick lines first.");
    }

    [Test]
    public async Task PreviewProfile_WithoutAToposolid_AsksForOne()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, new Curve[] { Ln(0, 0, 100, 0) });
        viewModel.DrapeOnTopo = true;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("toposolid")).IsTrue();
    }

    [Test]
    public async Task PreviewProfile_ZeroInterval_IsRejected()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 0m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("interval")).IsTrue();
    }

    [Test]
    public async Task PreviewProfile_NegativeTolerance_IsRejected()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.VerticalTolerance = -1m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("tolerance")).IsTrue();
    }

    [Test]
    public async Task PreviewProfile_LineOffTheToposolid_ExplainsWhy()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(500, 500, 600, 500) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 10m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("outside")).IsTrue();
    }

    [Test]
    public async Task PreviewProfile_OnFlatGround_ReportsTwoPvisAndNoGrade()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10), Ln(90, 10, 90, 60) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 10m;
        viewModel.VerticalTolerance = 0.1m;

        WithInvariantCulture(() => viewModel.PreviewProfileCommand.Execute(null));

        await Assert.That(viewModel.ProfilePreview.Contains("2 PVIs")).IsTrue();
        await Assert.That(viewModel.ProfilePreview.Contains("10.00 to 10.00")).IsTrue();
    }

    [Test]
    public async Task PreviewProfile_AppliesTheElevationOffset()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 10m;
        viewModel.ElevationOffset = 2.5m;

        WithInvariantCulture(() => viewModel.PreviewProfileCommand.Execute(null));

        await Assert.That(viewModel.ProfilePreview.Contains("12.50 to 12.50")).IsTrue();
    }

    [Test]
    public async Task ChangingAnyDrapeInput_ClearsTheStalePreview()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        var changes = new Action[]
        {
            () => viewModel.SampleInterval += 1m,
            () => viewModel.VerticalTolerance += 1m,
            () => viewModel.ElevationOffset += 1m,
            () => viewModel.MinVerticalCurveLength += 1m,
            () => viewModel.ReverseDirection = !viewModel.ReverseDirection,
            () => viewModel.DrapeOnTopo = !viewModel.DrapeOnTopo
        };

        foreach (var change in changes)
        {
            viewModel.ProfilePreview = "old preview";
            change();
            await Assert.That(viewModel.ProfilePreview).IsEqualTo(string.Empty);
        }
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [Test]
    public async Task CreateAlignment_DrapedOnFlatGround_WritesAFlatProfileAtGroundLevel()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        viewModel.AlignmentName = "Draped Road";
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10), Ln(90, 10, 90, 60) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 10m;
        viewModel.VerticalTolerance = 0.1m;
        viewModel.MinVerticalCurveLength = 0m;
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.CreateAlignmentCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request).IsNotNull();
        await Assert.That(request.PlanLength).IsEqualTo(130.0).Within(1e-6);

        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var expected = internalToShared.OfPoint(new XYZ(0, 0, 10)).Z;
        var profile = Curves(ProfileGeometry(Parse(request.Json)));
        await Assert.That(profile.Count).IsEqualTo(1);
        await Assert.That(PointOf(profile[0]!, Point2DKey, "position").Y).IsEqualTo(expected).Within(1e-6);
        await Assert.That(PointOf(profile[0]!, Vector2DKey, "direction").X).IsEqualTo(130.0).Within(1e-6);
    }

    [Test]
    public async Task CreateAlignment_DrapedWithOffset_RaisesTheProfile()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 10m;
        viewModel.ElevationOffset = 3m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var expected = internalToShared.OfPoint(new XYZ(0, 0, 13)).Z;
        var profile = Curves(ProfileGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(PointOf(profile[0]!, Point2DKey, "position").Y).IsEqualTo(expected).Within(1e-6);
    }

    [Test]
    public async Task CreateAlignment_WithDrapeOff_WritesTheFlatProfileAsBefore()
    {
        using var backup = new RegistryBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, new Curve[] { Ln(0, 0, 100, 0, 7.5) });
        viewModel.DrapeOnTopo = false;

        viewModel.CreateAlignmentCommand.Execute(null);

        var profile = Curves(ProfileGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(profile.Count).IsEqualTo(1);
        await Assert.That(PointOf(profile[0]!, Vector2DKey, "direction").Y).IsEqualTo(0.0);
    }

    [Test]
    public async Task CreateAlignment_SavesTheDrapeOptions()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.SampleInterval = 12.5m;
        viewModel.VerticalTolerance = 0.2m;
        viewModel.ElevationOffset = 1.5m;
        viewModel.MinVerticalCurveLength = 30m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var saved = new Settings();
        saved.LoadSettings();
        await Assert.That(saved.CreateAlignmentDrapeOnTopo).IsTrue();
        await Assert.That((double)saved.CreateAlignmentSampleInterval).IsEqualTo(12.5).Within(1e-6);
        await Assert.That((double)saved.CreateAlignmentVerticalTolerance).IsEqualTo(0.2).Within(1e-6);
        await Assert.That((double)saved.CreateAlignmentElevationOffset).IsEqualTo(1.5).Within(1e-6);
        await Assert.That((double)saved.CreateAlignmentMinVerticalCurveLength).IsEqualTo(30.0).Within(1e-6);
    }

    [Test]
    public async Task CreateAlignment_ReversedChain_IsSampledFromTheReversedStart()
    {
        using var backup = new RegistryBackup();
        var document = NewDocument();
        var viewModel = NewViewModel(document);
        UseFeet(viewModel);
        SetChain(viewModel, new Curve[] { Ln(10, 10, 90, 10), Ln(90, 10, 90, 60) });
        SetTopography(viewModel, BoxTopography(document, 100, 100, 10));
        viewModel.DrapeOnTopo = true;
        viewModel.ReverseDirection = true;
        viewModel.SampleInterval = 10m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var horizontal = Curves(HorizontalGeometry(Parse(viewModel.Request.Json)));
        var start = PointOf(horizontal[0]!, Point2DKey, "position");
        // reversed: the first curve now starts at the old end (90, 60) and runs south
        await Assert.That(start.Y).IsGreaterThan(50.0);
        await Assert.That(viewModel.Request.PlanLength).IsEqualTo(130.0).Within(1e-6);
    }

    // ---- the window ---------------------------------------------------------------------------------------------

    [Test]
    public async Task CreateAlignmentView_HidesItselfWhenAToposolidPickIsRequested()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new CreateAlignmentViewModel();
        Visibility visibility = Visibility.Visible;

        RunOnStaThread(() =>
        {
            var view = new CreateAlignmentView(viewModel);
            viewModel.PickToposolidCommand.Execute(null);
            visibility = view.Visibility;
        });

        await Assert.That(viewModel.PickToposolidRequested).IsTrue();
        await Assert.That(visibility).IsEqualTo(Visibility.Hidden);
    }

    [Test]
    public async Task CreateAlignmentView_ShowsTheDrapeControlsAndBindsThem()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new CreateAlignmentViewModel();
        var buttons = new List<string>();

        RunOnStaThread(() =>
        {
            var view = new CreateAlignmentView(viewModel);

            foreach (var button in FindChildren<System.Windows.Controls.Button>(view))
            {
                buttons.Add(button.Content?.ToString());
            }
        });

        await Assert.That(buttons.Contains("Pick lines/arcs")).IsTrue();
        await Assert.That(buttons.Contains("Pick toposolid")).IsTrue();
        await Assert.That(buttons.Contains("Preview profile")).IsTrue();
        await Assert.That(buttons.Contains("Create Alignment")).IsTrue();
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T match)
            {
                yield return match;
            }

            if (child is DependencyObject nested)
            {
                foreach (var descendant in FindChildren<T>(nested))
                {
                    yield return descendant;
                }
            }
        }
    }
}
