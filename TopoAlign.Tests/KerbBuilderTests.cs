using Autodesk.Revit.DB;
using TopoAlign.Sweep;

namespace TopoAlign.Tests;

public class SweepKerbPathTests : TopoAlignTest
{
    private static Line L(double x0, double y0, double x1, double y1, double z0 = 0, double z1 = 0)
        => Line.CreateBound(new XYZ(x0, y0, z0), new XYZ(x1, y1, z1));

    [Test]
    public async Task Build_EdgesInAnyOrderAndDirection_MakeOneRun()
    {
        var chains = SweepKerbPath.Build(new Curve[] { L(20, 0, 10, 0), L(0, 0, 10, 0), L(20, 0, 30, 5) }, 3.0, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(chains.Count).IsEqualTo(1);
        await Assert.That(chains[0].Points.Count).IsEqualTo(4);
        await Assert.That(chains[0].Closed).IsFalse();
        await Assert.That(chains[0].Length).IsEqualTo(10.0 + 10.0 + Math.Sqrt(125.0)).Within(1e-6);
    }

    [Test]
    public async Task Build_AnEdgePickedTwice_IsUsedOnce()
    {
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 10, 0), L(10, 0, 0, 0), L(10, 0, 20, 0) }, 3.0, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(chains.Count).IsEqualTo(1);
        await Assert.That(chains[0].CurveCount).IsEqualTo(2);
    }

    [Test]
    public async Task Build_EdgesThatDoNotTouch_MakeSeparateRuns()
    {
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 10, 0), L(50, 0, 60, 0) }, 3.0, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(chains.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Build_ABranch_IsRefusedWithAReason()
    {
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 10, 0), L(10, 0, 20, 0), L(10, 0, 10, 10) }, 3.0, out var error);

        await Assert.That(chains.Count).IsEqualTo(0);
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task Build_AClosedLoop_GoesAnticlockwiseWhicheverWayItWasPicked()
    {
        var clockwise = new Curve[] { L(0, 0, 0, 10), L(0, 10, 10, 10), L(10, 10, 10, 0), L(10, 0, 0, 0) };

        var chains = SweepKerbPath.Build(clockwise, 3.0, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(chains.Count).IsEqualTo(1);
        await Assert.That(chains[0].Closed).IsTrue();
        await Assert.That(chains[0].Points.Count).IsEqualTo(4);
        await Assert.That(SweepKerbPath.SignedArea(chains[0].Points) > 0).IsTrue();
    }

    [Test]
    public async Task Build_ACurvedEdge_IsCutIntoPiecesNoLongerThanTheSpacing()
    {
        var arc = Arc.Create(new XYZ(0, 0, 0), new XYZ(100, 100, 0), new XYZ(100 * Math.Sin(Math.PI / 4), 100 * (1 - Math.Cos(Math.PI / 4)), 0));

        var chains = SweepKerbPath.Build(new Curve[] { arc }, 10.0, out var error);

        await Assert.That(error).IsNull();
        await Assert.That(chains[0].Points.Count).IsGreaterThanOrEqualTo(Convert.ToInt32(Math.Ceiling(arc.Length / 10.0)) + 1);
        var points = chains[0].Points;
        for (var i = 1; i < points.Count; i++)
        {
            await Assert.That(PathPoint.Distance(points[i - 1], points[i])).IsLessThan(10.5);
        }
    }

    [Test]
    public async Task Build_NothingPicked_SaysSo()
    {
        var chains = SweepKerbPath.Build(new List<Curve>(), 3.0, out var error);

        await Assert.That(chains.Count).IsEqualTo(0);
        await Assert.That(error).IsNotNull();
    }
}

public class KerbBuilderTests : TopoAlignTest
{
    private static Line L(double x0, double y0, double x1, double y1, double z0 = 0, double z1 = 0)
        => Line.CreateBound(new XYZ(x0, y0, z0), new XYZ(x1, y1, z1));

    private static (double Volume, int Skipped, int Runs) Solids(IEnumerable<Curve> curves)
    {
        var chains = SweepKerbPath.Build(curves, 5.0, out var error);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        // the slab is 12 ft by 6 in, so 6 square feet, centred on the edge
        var solids = KerbBuilder.BuildSolids(chains, RoadFixtures.Slab(), 0.0, out var runs, out _, out _);
        return (solids.Solids.Sum(s => s.Volume), solids.SkippedSpans, runs);
    }

    [Test]
    public async Task BuildSolids_AStraightEdge_HasTheVolumeOfAreaTimesLength()
    {
        var (volume, skipped, runs) = Solids(new Curve[] { L(0, 0, 100, 0) });

        await Assert.That(skipped).IsEqualTo(0);
        await Assert.That(runs).IsEqualTo(1);
        await Assert.That(volume).IsEqualTo(600.0).Within(0.01);
    }

    [Test]
    public async Task BuildSolids_ARightAngleCorner_IsMitredSoTheKerbKeepsItsWidth()
    {
        // a symmetrical section: the mitre makes one side longer and the other shorter by the same amount
        var (volume, skipped, runs) = Solids(new Curve[] { L(0, 0, 100, 0), L(100, 0, 100, 100) });

        await Assert.That(skipped).IsEqualTo(0);
        await Assert.That(runs).IsEqualTo(1);
        await Assert.That(volume).IsEqualTo(6.0 * 200.0).Within(0.5);
    }

    [Test]
    public async Task BuildSolids_ASlopingEdge_KeepsTheProfileVerticalSoTheVolumeFollowsThePlanLength()
    {
        // vertical sections slide up the slope, so the volume is area x plan length, not area x slope length
        var (volume, skipped, _) = Solids(new Curve[] { L(0, 0, 100, 0, 0, 20) });

        await Assert.That(skipped).IsEqualTo(0);
        await Assert.That(volume).IsEqualTo(600.0).Within(0.5);
    }

    [Test]
    public async Task BuildSolids_AClosedLoop_IsOneRunWithNoGap()
    {
        var (volume, skipped, runs) = Solids(new Curve[] { L(0, 0, 100, 0), L(100, 0, 100, 100), L(100, 100, 0, 100), L(0, 100, 0, 0) });

        await Assert.That(skipped).IsEqualTo(0);
        await Assert.That(runs).IsEqualTo(1);
        await Assert.That(volume).IsEqualTo(6.0 * 400.0).Within(1.0);
    }

    [Test]
    public async Task BuildSolids_ASharpCorner_SplitsTheKerbAndStillBuilds()
    {
        var (volume, skipped, runs) = Solids(new Curve[] { L(0, 0, 100, 0), L(100, 0, 0, 40) });

        await Assert.That(runs).IsEqualTo(2);
        await Assert.That(skipped).IsEqualTo(0);
        await Assert.That(volume).IsGreaterThan(6.0 * 150.0);
    }

    [Test]
    public async Task Create_AsADirectShape_AddsOneTaggedShapeThatIsNotARoad()
    {
        var document = NewDocument();
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 50, 0), L(50, 0, 50, 50) }, 5.0, out _);

        var result = KerbBuilder.Create(document, chains, RoadFixtures.Slab(), new KerbOptions { Output = RoadOutput.DirectShape, ProfileName = "Test : Slab" });

        var shape = document.GetElement(result.KerbId);
        await Assert.That(shape is DirectShape).IsTrue();
        await Assert.That(KerbBuilder.IsKerb(shape)).IsTrue();
        await Assert.That(RoadBuilder.IsRoad(shape)).IsFalse();
        await Assert.That(RoadFixtures.Volume(shape)).IsEqualTo(6.0 * 100.0).Within(0.5);
        await Assert.That(result.SkippedSpans).IsEqualTo(0);
    }

    /// <summary>A 6 ft wide, 6 inch thick slab hanging down from the edge towards +u only.</summary>
    private static SectionProfile OneSided()
    {
        var segments = new List<TopoAlign.Alignments.PlanSegment>
        {
            new TopoAlign.Alignments.PlanLine(new TopoAlign.Alignments.PlanPoint(0, 0), new TopoAlign.Alignments.PlanPoint(6, 0)),
            new TopoAlign.Alignments.PlanLine(new TopoAlign.Alignments.PlanPoint(6, 0), new TopoAlign.Alignments.PlanPoint(6, -0.5)),
            new TopoAlign.Alignments.PlanLine(new TopoAlign.Alignments.PlanPoint(6, -0.5), new TopoAlign.Alignments.PlanPoint(0, -0.5)),
            new TopoAlign.Alignments.PlanLine(new TopoAlign.Alignments.PlanPoint(0, -0.5), new TopoAlign.Alignments.PlanPoint(0, 0))
        };
        SectionProfile.TryCreate(segments, out var profile, out var error);
        return profile ?? throw new InvalidOperationException(error);
    }

    [Test]
    public async Task Create_ReviewedAndFlipped_EndsOnTheOtherSideAndLeavesOnlyOneKerb()
    {
        var document = NewDocument();
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 50, 0) }, 5.0, out _);
        var seen = new List<double>();
        var options = new KerbOptions
        {
            Review = kerb =>
            {
                var ys = document.GetElement(kerb.KerbId).get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine })
                    .OfType<Solid>()
                    .Select(solid => solid.GetBoundingBox())
                    .SelectMany(box => new[] { box.Transform.OfPoint(box.Min).Y, box.Transform.OfPoint(box.Max).Y })
                    .ToList();
                seen.Add((ys.Min() + ys.Max()) / 2.0);
                return seen.Count == 1 ? KerbReviewChoice.Flip : KerbReviewChoice.Keep;
            }
        };

        var result = KerbBuilder.Create(document, chains, OneSided(), options);

        await Assert.That(result.Flipped).IsTrue();
        await Assert.That(result.Cancelled).IsFalse();
        await Assert.That(seen.Count).IsEqualTo(2);
        // heading east, the right-hand side (+u) is south, then the flip puts it north
        await Assert.That(seen[0]).IsEqualTo(-3.0).Within(0.01);
        await Assert.That(seen[1]).IsEqualTo(3.0).Within(0.01);
        await Assert.That(new FilteredElementCollector(document).OfClass(typeof(DirectShape)).Cast<DirectShape>().Count(KerbBuilder.IsKerb)).IsEqualTo(1);
    }

    [Test]
    public async Task Create_ReviewCancelled_LeavesNothingBehind()
    {
        var document = NewDocument();
        var chains = SweepKerbPath.Build(new Curve[] { L(0, 0, 50, 0) }, 5.0, out _);

        var result = KerbBuilder.Create(document, chains, OneSided(), new KerbOptions { Review = _ => KerbReviewChoice.Cancel });

        await Assert.That(result.Cancelled).IsTrue();
        await Assert.That(new FilteredElementCollector(document).OfClass(typeof(DirectShape)).Cast<DirectShape>().Count(KerbBuilder.IsKerb)).IsEqualTo(0);
    }

    [Test]
    public async Task Create_NothingPicked_Throws()
    {
        var document = NewDocument();

        var exception = TestKit.Catch(() => KerbBuilder.Create(document, new List<KerbChain>(), RoadFixtures.Slab(), new KerbOptions()));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }
}

public class KerbFamilyTests : TopoAlignTest
{
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
    public async Task Create_AsAFamily_LoadsAKerbFamilyAndPlacesOneInstance()
    {
        var document = NewDocument();
        var chains = SweepKerbPath.Build(new Curve[] { Line.CreateBound(new XYZ(0, 0, 0), new XYZ(50, 0, 0)), Line.CreateBound(new XYZ(50, 0, 0), new XYZ(50, 50, 0)) }, 5.0, out _);

        var result = KerbBuilder.Create(document, chains, RoadFixtures.Slab(), new KerbOptions { Output = RoadOutput.Family, ProfileName = "Test : Slab" });

        var instance = document.GetElement(result.KerbId) as FamilyInstance;
        await Assert.That(instance is null).IsFalse();
        await Assert.That(KerbBuilder.IsKerb(instance)).IsTrue();
        await Assert.That(RoadBuilder.IsRoad(instance)).IsFalse();
        await Assert.That(instance!.Symbol.Family.Name.StartsWith("TopoAlign Kerb - ")).IsTrue();
        var volume = 0.0;
        foreach (var item in instance.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }))
        {
            if (item is GeometryInstance geometryInstance)
            {
                volume += geometryInstance.GetInstanceGeometry().OfType<Solid>().Sum(solid => solid.Volume);
            }
        }

        await Assert.That(volume).IsEqualTo(6.0 * 100.0).Within(1.0);
    }
}
