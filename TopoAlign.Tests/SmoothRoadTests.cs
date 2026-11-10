using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.Alignments.Profile;
using TopoAlign.Sweep.Profiles;
using TopoAlign.Sweep.Road;
using TopoAlign.ViewModels.RoadTools;
using Settings = TopoAlign.Services.Settings;

namespace TopoAlign.Tests;

public class SmoothRoadPlanTests : TopoAlignTest
{
    [Test]
    public async Task Ring_ASlabDrawnClockwise_IsFourCornersTurnedAnticlockwise()
    {
        var ring = SmoothRoadPlan.Ring(RoadFixtures.Slab());

        await Assert.That(ring.Count).IsEqualTo(4);
        await Assert.That(SmoothRoadPlan.SignedArea(ring)).IsEqualTo(6.0).Within(1e-9);
    }

    [Test]
    public async Task Ring_AnAnticlockwiseSection_IsKeptAnticlockwise()
    {
        var segments = new List<PlanSegment>
        {
            new PlanLine(new PlanPoint(-6, 0), new PlanPoint(-6, -0.5)),
            new PlanLine(new PlanPoint(-6, -0.5), new PlanPoint(6, -0.5)),
            new PlanLine(new PlanPoint(6, -0.5), new PlanPoint(6, 0)),
            new PlanLine(new PlanPoint(6, 0), new PlanPoint(-6, 0))
        };
        SectionProfile.TryCreate(segments, out var anticlockwise, out _);

        var ring = SmoothRoadPlan.Ring(anticlockwise);

        await Assert.That(SmoothRoadPlan.SignedArea(ring)).IsGreaterThan(0.0);
    }

    [Test]
    public async Task Ring_ARoundedSection_CutsTheArcIntoShortStraights()
    {
        var ring = SmoothRoadPlan.Ring(RoadFixtures.Crowned());

        // a half circle in steps of at most 5 degrees is 36 pieces
        await Assert.That(ring.Count).IsGreaterThanOrEqualTo(36);
        await Assert.That(SmoothRoadPlan.SignedArea(ring)).IsEqualTo(RoadFixtures.Crowned().Area).Within(RoadFixtures.Crowned().Area * 0.01);
    }

    [Test]
    public async Task Ring_NullProfile_Throws()
    {
        await Assert.That(Catch(() => SmoothRoadPlan.Ring(null)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Chunks_WithoutJoins_IsOnePiece()
    {
        var stations = Enumerable.Range(0, 11).Select(i => i * 10.0).ToList();

        var chunks = SmoothRoadPlan.Chunks(stations, null, 20.0);

        await Assert.That(chunks.Count).IsEqualTo(1);
        await Assert.That(chunks[0]).IsEqualTo((0, 10));
    }

    [Test]
    public async Task Chunks_SplitAtJoinsFarEnoughApart_AndShareTheJoin()
    {
        var stations = Enumerable.Range(0, 31).Select(i => i * 10.0).ToList();

        var chunks = SmoothRoadPlan.Chunks(stations, new[] { 100.0, 200.0 }, 50.0);

        await Assert.That(chunks.Count).IsEqualTo(3);
        await Assert.That(chunks[0]).IsEqualTo((0, 10));
        await Assert.That(chunks[1]).IsEqualTo((10, 20));
        await Assert.That(chunks[2]).IsEqualTo((20, 30));
    }

    [Test]
    public async Task Chunks_JoinsCloserThanTheMinimum_AreMergedAway()
    {
        var stations = Enumerable.Range(0, 31).Select(i => i * 10.0).ToList();

        // joins at 100, 110, 120: only the first is far enough from the start, the rest are too close to it
        var chunks = SmoothRoadPlan.Chunks(stations, new[] { 100.0, 110.0, 120.0 }, 50.0);

        await Assert.That(chunks.Count).IsEqualTo(2);
        await Assert.That(chunks[0]).IsEqualTo((0, 10));
        await Assert.That(chunks[1]).IsEqualTo((10, 30));
    }

    [Test]
    public async Task Chunks_AJoinNearTheEnd_IsIgnored()
    {
        var stations = Enumerable.Range(0, 11).Select(i => i * 10.0).ToList();

        var chunks = SmoothRoadPlan.Chunks(stations, new[] { 90.0 }, 50.0);

        await Assert.That(chunks.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Chunks_NotEnoughStations_GivesNothing()
    {
        await Assert.That(SmoothRoadPlan.Chunks(null, null, 10.0).Count).IsEqualTo(0);
        await Assert.That(SmoothRoadPlan.Chunks(new List<double> { 0.0 }, null, 10.0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task MaxCurvature_AQuarterTurn_IsTheReciprocalOfTheRadius()
    {
        var radius = 100.0;
        var stations = new List<double>();
        var tangents = new List<(double X, double Y)>();
        for (var i = 0; i <= 10; i++)
        {
            var angle = Math.PI / 2 * i / 10;
            stations.Add(radius * angle);
            tangents.Add((Math.Cos(angle), Math.Sin(angle)));
        }

        var curvature = SmoothRoadPlan.MaxCurvature(stations, tangents);

        await Assert.That(curvature).IsEqualTo(1.0 / radius).Within(1e-9);
    }

    [Test]
    public async Task MaxCurvature_AStraightRun_IsZero()
    {
        var stations = new List<double> { 0, 10, 20 };
        var tangents = new List<(double X, double Y)> { (1, 0), (1, 0), (1, 0) };

        await Assert.That(SmoothRoadPlan.MaxCurvature(stations, tangents)).IsEqualTo(0.0).Within(1e-12);
    }
}

public class SmoothRoadBuilderTests : TopoAlignTest
{
    private static RoadOptions Smooth(double interval = 10.0) => new() { Interval = interval, GeometryMethod = RoadGeometryMethod.SmoothSurfaces };

    private static int FaceCount(Element shape)
    {
        return shape.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }).OfType<Solid>().Sum(s => s.Faces.Size);
    }

    [Test]
    public async Task Create_Smooth_MakesAContinuousRoadWithTheExpectedVolume()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        await Assert.That(result.Notice).IsEqualTo(string.Empty);
        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        var road = document.GetElement(result.RoadId);
        await Assert.That(road is DirectShape).IsTrue();
        await Assert.That(RoadFixtures.Volume(road)).IsEqualTo(6.0 * RoadFixtures.CentrelineLength).Within(6.0 * RoadFixtures.CentrelineLength * 0.01);
    }

    [Test]
    public async Task Create_Smooth_HasFarFewerFacesThanSegments()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var segmented = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 10.0 });
        var segmentedFaces = FaceCount(document.GetElement(segmented.RoadId));

        var smooth = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        var smoothFaces = FaceCount(document.GetElement(smooth.RoadId));
        await Assert.That(smooth.SolidCount).IsLessThanOrEqualTo(3);
        await Assert.That(smoothFaces).IsLessThanOrEqualTo(smooth.SolidCount * 6);
        await Assert.That(smoothFaces * 5).IsLessThan(segmentedFaces);
    }

    [Test]
    public async Task Create_Smooth_AStraightRunIsOneSolidOfSixFaces()
    {
        var document = NewDocument();
        var segments = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(300, 0, 0, 0) }).Segments;
        var json = AlignmentJsonBuilder.BuildFromSegments("Straight", segments, document.ActiveProjectLocation.GetTotalTransform().Inverse, 0.0, 0.0, null, 0.0, 0.005, out var length);
        var alignment = document.GetElement(AlignmentApi.Create(document, "Straight", json, length));

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        await Assert.That(result.Notice).IsEqualTo(string.Empty);
        await Assert.That(result.SolidCount).IsEqualTo(1);
        await Assert.That(FaceCount(document.GetElement(result.RoadId))).IsEqualTo(6);
        await Assert.That(RoadFixtures.Volume(document.GetElement(result.RoadId))).IsEqualTo(6.0 * 300.0).Within(0.05);
    }

    [Test]
    public async Task Create_Smooth_ARoundedSection_KeepsItsArea()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var crowned = RoadFixtures.Crowned();

        var result = RoadBuilder.Create(document, alignment, crowned, Smooth());

        await Assert.That(result.Notice).IsEqualTo(string.Empty);
        var expected = crowned.Area * RoadFixtures.CentrelineLength;
        await Assert.That(RoadFixtures.Volume(document.GetElement(result.RoadId))).IsEqualTo(expected).Within(expected * 0.02);
    }

    [Test]
    public async Task Create_Smooth_FollowsAGradedProfile()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document, "Climbing", new List<ProfilePoint> { new(0, 0), new(RoadFixtures.CentrelineLength, 10) });

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        await Assert.That(result.Notice).IsEqualTo(string.Empty);
        var box = document.GetElement(result.RoadId).get_BoundingBox(null);
        await Assert.That(box.Max.Z - box.Min.Z).IsEqualTo(10.5).Within(0.05);
    }

    [Test]
    public async Task Create_Smooth_AProfileTooWideForTheTurn_FallsBackAndSaysSo()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        RoadResult result = null;
        var exception = Catch(() => result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(400, 0.5), Smooth(20.0)));

        if (exception is null)
        {
            await Assert.That(result.Notice.Contains("segments")).IsTrue();
        }
        else
        {
            await Assert.That(exception is InvalidOperationException).IsTrue();
        }
    }

    [Test]
    public async Task Create_Smooth_Twice_ReplacesTheFirstRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var first = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        var second = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), Smooth());

        await Assert.That(second.ReplacedPrevious).IsTrue();
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
        await Assert.That(document.GetElement(first.RoadId)).IsNull();
    }
}

public class RoadFamilyTests : TopoAlignTest
{
    /// <summary>
    /// Uses the Generic Model template installed with this Revit. Where there is none (some installs only carry a few templates),
    /// any other installed Revit's template does, since a newer Revit upgrades an older template when it opens it.
    /// </summary>
    [Before(Test)]
    public void FindATemplate()
    {
        RoadFamilyBuilder.TemplateOverride = null;
        if (RoadFamilyBuilder.FindTemplate(Application) is not null)
        {
            return;
        }

        var root = @"C:\ProgramData\Autodesk";
        var found = System.IO.Directory.Exists(root)
            ? System.IO.Directory.EnumerateDirectories(root, "RVT 20*")
                .OrderByDescending(d => d)
                .SelectMany(d => System.IO.Directory.EnumerateFiles(d, "Generic Model.rft", System.IO.SearchOption.AllDirectories))
                .FirstOrDefault()
            : null;
        if (found is null)
        {
            Skip.Test("No Generic Model family template is installed.");
        }

        RoadFamilyBuilder.TemplateOverride = found;
    }

    [After(Test)]
    public void ForgetTheTemplate()
    {
        RoadFamilyBuilder.TemplateOverride = null;
    }

    [Test]
    [Arguments("Metric Generic Model.rft")]
    [Arguments("Generic Model.rft")]
    public async Task FindTemplateIn_RecognisesMetricAndImperialNames(string name)
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TopoAlignTemplateTest", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            // the variants must not be picked even though they sort before the plain template
            foreach (var other in new[] { "Metric Generic Model Adaptive.rft", "Metric Generic Model face based.rft", "Generic Model wall based.rft", name })
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, other), string.Empty);
            }

            var found = RoadFamilyBuilder.FindTemplateIn(folder);

            await Assert.That(System.IO.Path.GetFileName(found)).IsEqualTo(name);
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task FindTemplateIn_NoPlainTemplate_ReturnsNull()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TopoAlignTemplateTest", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "Metric Generic Model face based.rft"), string.Empty);

            await Assert.That(RoadFamilyBuilder.FindTemplateIn(folder)).IsNull();
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    private static RoadOptions AsFamily(RoadGeometryMethod method = RoadGeometryMethod.SegmentedLoft, double interval = 10.0)
        => new() { Interval = interval, GeometryMethod = method, Output = RoadOutput.Family, ProfileName = "Slab : 12 ft" };

    private static double InstanceVolume(FamilyInstance instance)
    {
        var total = 0.0;
        var geometry = instance.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
        foreach (var item in geometry)
        {
            if (item is GeometryInstance geometryInstance)
            {
                total += geometryInstance.GetInstanceGeometry().OfType<Solid>().Sum(s => s.Volume);
            }
            else if (item is Solid solid)
            {
                total += solid.Volume;
            }
        }

        return total;
    }

    private static List<Family> RoadFamilies(Document document)
    {
        return new FilteredElementCollector(document).OfClass(typeof(Family)).Cast<Family>().Where(f => f.Name.StartsWith("TopoAlign Road - ")).ToList();
    }

    [Test]
    public async Task FamilyName_IsStableCleanAndTiedToTheAlignment()
    {
        var document = NewDocument();
        var one = RoadFixtures.CurvedAlignment(document, "A:B");
        var two = RoadFixtures.CurvedAlignment(document, "A:B");

        var name = RoadFamilyBuilder.FamilyName(one, "A:B");

        await Assert.That(name.StartsWith("TopoAlign Road - A_B (")).IsTrue();
        await Assert.That(name).IsEqualTo(RoadFamilyBuilder.FamilyName(one, "A:B"));
        await Assert.That(name).IsNotEqualTo(RoadFamilyBuilder.FamilyName(two, "A:B"));
        await Assert.That(RoadFamilyBuilder.FamilyName(one, " ").StartsWith("TopoAlign Road - Alignment (")).IsTrue();
    }

    [Test]
    public async Task Create_AsFamily_MakesOneFamilyAndOneInstanceWithTheExpectedVolume()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily());

        var instance = document.GetElement(result.RoadId) as FamilyInstance;
        await Assert.That(instance).IsNotNull();
        await Assert.That(result.FamilyId).IsNotEqualTo(ElementId.InvalidElementId);
        await Assert.That(RoadFamilies(document).Count).IsEqualTo(1);
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
        await Assert.That(InstanceVolume(instance)).IsEqualTo(6.0 * RoadFixtures.CentrelineLength).Within(6.0 * RoadFixtures.CentrelineLength * 0.02);
    }

    [Test]
    public async Task Create_AsFamily_SitsWhereTheGeometryWasBuilt()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document, "Placed", new List<ProfilePoint> { new(0, 20), new(RoadFixtures.CentrelineLength, 30) });

        var asShape = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 10.0 });
        var shapeBox = document.GetElement(asShape.RoadId).get_BoundingBox(null);

        var asFamily = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily());
        var familyBox = document.GetElement(asFamily.RoadId).get_BoundingBox(null);

        await Assert.That(familyBox.Min.X).IsEqualTo(shapeBox.Min.X).Within(0.01);
        await Assert.That(familyBox.Max.X).IsEqualTo(shapeBox.Max.X).Within(0.01);
        await Assert.That(familyBox.Min.Y).IsEqualTo(shapeBox.Min.Y).Within(0.01);
        await Assert.That(familyBox.Min.Z).IsEqualTo(shapeBox.Min.Z).Within(0.01);
        await Assert.That(familyBox.Max.Z).IsEqualTo(shapeBox.Max.Z).Within(0.01);
    }

    [Test]
    public async Task Create_AsFamily_Smooth_IsOneContinuousRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily(RoadGeometryMethod.SmoothSurfaces));

        await Assert.That(result.Notice).IsEqualTo(string.Empty);
        await Assert.That(result.SolidCount).IsLessThanOrEqualTo(3);
        await Assert.That(InstanceVolume((FamilyInstance)document.GetElement(result.RoadId))).IsEqualTo(6.0 * RoadFixtures.CentrelineLength)
            .Within(6.0 * RoadFixtures.CentrelineLength * 0.02);
    }

    [Test]
    public async Task Create_AsFamily_Twice_ReplacesTheFamilyAndItsInstance()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var first = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily());

        var second = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(8, 1), AsFamily());

        await Assert.That(second.ReplacedPrevious).IsTrue();
        await Assert.That(RoadFamilies(document).Count).IsEqualTo(1);
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
        await Assert.That(document.GetElement(first.RoadId)).IsNull();
        await Assert.That(document.GetElement(second.RoadId)).IsNotNull();
    }

    [Test]
    public async Task Create_SwitchingBetweenShapeAndFamily_ReplacesTheEarlierRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });

        var family = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily(interval: 25.0));
        await Assert.That(family.ReplacedPrevious).IsTrue();
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);

        var shape = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });
        await Assert.That(shape.ReplacedPrevious).IsTrue();
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
        await Assert.That(RoadFamilies(document).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Create_AsFamily_ForDifferentAlignments_KeepsBothFamilies()
    {
        var document = NewDocument();
        var one = RoadFixtures.CurvedAlignment(document, "One");
        var two = RoadFixtures.CurvedAlignment(document, "Two");

        RoadBuilder.Create(document, one, RoadFixtures.Slab(), AsFamily(interval: 25.0));
        var result = RoadBuilder.Create(document, two, RoadFixtures.Slab(), AsFamily(interval: 25.0));

        await Assert.That(result.ReplacedPrevious).IsFalse();
        await Assert.That(RoadFamilies(document).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Create_AsFamily_TagsTheInstanceWithAComment()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document, "Commented");

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily(interval: 25.0));

        var comments = document.GetElement(result.RoadId).get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
        await Assert.That(comments.Contains("Commented")).IsTrue();
        await Assert.That(comments.Contains("Slab : 12 ft")).IsTrue();
    }

    [Test]
    public async Task Create_AsFamily_LeavesNoTemporaryFilesBehind()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TopoAlign");
        var before = System.IO.Directory.Exists(root) ? System.IO.Directory.GetFiles(root, "*.rfa", System.IO.SearchOption.AllDirectories).Length : 0;

        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), AsFamily(interval: 25.0));

        var after = System.IO.Directory.Exists(root) ? System.IO.Directory.GetFiles(root, "*.rfa", System.IO.SearchOption.AllDirectories).Length : 0;
        await Assert.That(after).IsEqualTo(before);
    }

    [Test]
    public async Task DeleteFile_ANullOrMissingFile_DoesNotThrow()
    {
        RoadFamilyBuilder.DeleteFile(null);
        RoadFamilyBuilder.DeleteFile(string.Empty);
        RoadFamilyBuilder.DeleteFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TopoAlign", "does-not-exist", "x.rfa"));

        await Assert.That(true).IsTrue();
    }
}

public class SweepGeometryOptionsTests : TopoAlignUiTest
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
    public async Task Defaults_KeepTodaysBehaviour()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();

        var viewModel = new SweepAlignmentViewModel();

        await Assert.That(viewModel.SelectedGeometryMethod.Value).IsEqualTo(RoadGeometryMethod.SegmentedLoft);
        await Assert.That(viewModel.SelectedOutput.Value).IsEqualTo(RoadOutput.DirectShape);
        await Assert.That(new RoadOptions().GeometryMethod).IsEqualTo(RoadGeometryMethod.SegmentedLoft);
        await Assert.That(new RoadOptions().Output).IsEqualTo(RoadOutput.DirectShape);
    }

    [Test]
    public async Task Create_PassesTheChosenOptionsOnAndRemembersThem()
    {
        using var backup = new SettingsBackup();
        var document = NewDocument();
        App.RevitDocument = document;
        var alignment = RoadFixtures.CurvedAlignment(document, "Options");
        var viewModel = new SweepAlignmentViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        typeof(SweepAlignmentViewModel).GetField("_profile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(viewModel, RoadFixtures.Slab());
        viewModel.SelectAlignment(alignment);
        viewModel.SelectedGeometryMethod = viewModel.GeometryMethods.First(m => m.Value == RoadGeometryMethod.SmoothSurfaces);
        viewModel.SelectedOutput = viewModel.Outputs.First(o => o.Value == RoadOutput.Family);

        viewModel.CreateCommand.Execute(null);

        await Assert.That(viewModel.Request.Options.GeometryMethod).IsEqualTo(RoadGeometryMethod.SmoothSurfaces);
        await Assert.That(viewModel.Request.Options.Output).IsEqualTo(RoadOutput.Family);
        var saved = new Settings();
        saved.LoadSettings();
        await Assert.That(saved.SweepGeometryMethod).IsEqualTo("SmoothSurfaces");
        await Assert.That(saved.SweepOutput).IsEqualTo("Family");

        var next = new SweepAlignmentViewModel();
        await Assert.That(next.SelectedGeometryMethod.Value).IsEqualTo(RoadGeometryMethod.SmoothSurfaces);
        await Assert.That(next.SelectedOutput.Value).IsEqualTo(RoadOutput.Family);
    }

    [Test]
    public async Task Settings_AnUnknownSavedValue_FallsBackToTheDefault()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();
        new Settings { SweepGeometryMethod = "Nonsense", SweepOutput = "Nonsense" }.SaveSettings();

        var viewModel = new SweepAlignmentViewModel();

        await Assert.That(viewModel.SelectedGeometryMethod.Value).IsEqualTo(RoadGeometryMethod.SegmentedLoft);
        await Assert.That(viewModel.SelectedOutput.Value).IsEqualTo(RoadOutput.DirectShape);
    }
}
