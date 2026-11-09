using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.IO;
using TopoAlign.Alignments;
using TopoAlign.Grading;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using Settings = TopoAlign.Models.Settings;

namespace TopoAlign.Tests;

internal static class AssemblyFixtures
{
    /// <summary>A rectangle from (u0, v0) to (u1, v1).</summary>
    public static SectionProfile Box(double u0, double v0, double u1, double v1)
    {
        var segments = new List<PlanSegment>
        {
            new PlanLine(new PlanPoint(u0, v0), new PlanPoint(u1, v0)),
            new PlanLine(new PlanPoint(u1, v0), new PlanPoint(u1, v1)),
            new PlanLine(new PlanPoint(u1, v1), new PlanPoint(u0, v1)),
            new PlanLine(new PlanPoint(u0, v1), new PlanPoint(u0, v0))
        };

        if (!SectionProfile.TryCreate(segments, out var profile, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return profile;
    }

    /// <summary>A kerb drawn for the left of the road: 0.5 ft wide, 0.5 ft high, from the origin outwards to the left (u from -0.5 to 0).</summary>
    public static SectionProfile LeftKerb() => Box(-0.5, 0.0, 0.0, 0.5);

    public static SweepAssembly RoadAndKerbs(double kerbOffsetMm = 6.25 * AssemblyPart.MillimetresPerFoot)
    {
        return new SweepAssembly
        {
            Name = "Road with kerbs",
            Parts = new List<AssemblyPart>
            {
                new() { Name = "Road", ProfileName = "Road", Category = "OST_GenericModel" },
                new() { Name = "Kerb (left)", ProfileName = "Kerb", LateralOffsetMm = -kerbOffsetMm, Category = "OST_GenericModel" },
                new() { Name = "Kerb (right)", ProfileName = "Kerb", LateralOffsetMm = kerbOffsetMm, MirrorProfile = true, Category = "OST_GenericModel" }
            }
        };
    }

    public static Dictionary<string, SectionProfile> Profiles() => new()
    {
        ["Road"] = RoadFixtures.Slab(12.0, 0.5),
        ["Kerb"] = LeftKerb()
    };
}

public class SectionProfileOffsetTests : TopoAlignTest
{
    [Test]
    public async Task Offset_MovesTheExtentsAndKeepsTheShape()
    {
        var kerb = AssemblyFixtures.Box(0, 0, 2, 1);

        var moved = kerb.Offset(5.0, -3.0);

        await Assert.That(moved.MinU).IsEqualTo(5.0).Within(1e-9);
        await Assert.That(moved.MaxU).IsEqualTo(7.0).Within(1e-9);
        await Assert.That(moved.MinV).IsEqualTo(-3.0).Within(1e-9);
        await Assert.That(moved.MaxV).IsEqualTo(-2.0).Within(1e-9);
        await Assert.That(moved.Area).IsEqualTo(kerb.Area).Within(1e-9);
        await Assert.That(moved.Segments.Count).IsEqualTo(kerb.Segments.Count);
    }

    [Test]
    public async Task Offset_MovesAnArcByMovingItsCentre()
    {
        // the semicircular crown of radius 3 spans u -3..3 and v up to 3
        var crowned = RoadFixtures.Crowned();

        var moved = crowned.Offset(10.0, 2.0);

        await Assert.That(moved.MinU).IsEqualTo(crowned.MinU + 10.0).Within(1e-6);
        await Assert.That(moved.MaxU).IsEqualTo(crowned.MaxU + 10.0).Within(1e-6);
        await Assert.That(moved.MaxV).IsEqualTo(crowned.MaxV + 2.0).Within(1e-6);
        await Assert.That(moved.Area).IsEqualTo(crowned.Area).Within(1e-6);
    }

    [Test]
    public async Task Offset_DoesNotChangeTheOriginal()
    {
        var kerb = AssemblyFixtures.Box(0, 0, 2, 1);

        kerb.Offset(5.0, 5.0);

        await Assert.That(kerb.MinU).IsEqualTo(0.0);
        await Assert.That(kerb.MaxV).IsEqualTo(1.0);
    }

    [Test]
    public async Task Place_MirrorsAboutTheProfileOriginAndThenOffsets()
    {
        // a kerb drawn on the left of its origin (u -0.5..0) used on the right of the road, 6 ft out
        var part = new AssemblyPart { LateralOffsetMm = 6.0 * AssemblyPart.MillimetresPerFoot, VerticalOffsetMm = 0.25 * AssemblyPart.MillimetresPerFoot, MirrorProfile = true };

        var placed = part.Place(AssemblyFixtures.LeftKerb());

        await Assert.That(placed.MinU).IsEqualTo(6.0).Within(1e-9);
        await Assert.That(placed.MaxU).IsEqualTo(6.5).Within(1e-9);
        await Assert.That(placed.MinV).IsEqualTo(0.25).Within(1e-9);
    }

    [Test]
    public async Task Place_WithoutMirror_OnlyOffsets()
    {
        var part = new AssemblyPart { LateralOffsetMm = -6.0 * AssemblyPart.MillimetresPerFoot };

        var placed = part.Place(AssemblyFixtures.LeftKerb());

        await Assert.That(placed.MinU).IsEqualTo(-6.5).Within(1e-9);
        await Assert.That(placed.MaxU).IsEqualTo(-6.0).Within(1e-9);
    }
}

public class AssemblyPreviewTests : TopoAlignTest
{
    [Test]
    public async Task Build_PlacesEachPartLikeTheBuilder()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles.TryGetValue(name, out var p) ? p : null);

        await Assert.That(preview.Outlines.Count).IsEqualTo(3);
        await Assert.That(preview.Missing.Count).IsEqualTo(0);

        for (var i = 0; i < 3; i++)
        {
            var expected = assembly.Parts[i].Place(profiles[assembly.Parts[i].ProfileName]).Outline();
            var actual = preview.Outlines[i].Points;
            await Assert.That(actual.Count).IsEqualTo(expected.Count);
            await Assert.That(actual.Min(p => p.X)).IsEqualTo(expected.Min(p => p.X)).Within(1e-9);
            await Assert.That(actual.Max(p => p.X)).IsEqualTo(expected.Max(p => p.X)).Within(1e-9);
        }
    }

    [Test]
    public async Task Build_GradedSidesGetACutAndAFillLineFromTheShoulder()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();
        var right = new SideGrading { Enabled = true, CutValue = 2, FillValue = 4 };

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles[name], new SideGrading(), right);

        // only the right side is on
        await Assert.That(preview.Slopes.Count).IsEqualTo(2);
        var cut = preview.Slopes.Single(s => s.IsCut);
        var fill = preview.Slopes.Single(s => !s.IsCut);
        await Assert.That(cut.Side).IsEqualTo(AssemblySide.Right);
        await Assert.That(cut.U).IsEqualTo(6.75).Within(1e-9);
        await Assert.That(cut.V).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(cut.Label).IsEqualTo("cut 1:2");
        await Assert.That(fill.Label).IsEqualTo("fill 1:4");

        // 1 : 2 climbs half a foot for each foot out; 1 : 4 falls a quarter
        await Assert.That(cut.HeightAt(8.75)).IsEqualTo(1.5).Within(1e-9);
        await Assert.That(fill.HeightAt(10.75)).IsEqualTo(-0.5).Within(1e-9);
    }

    [Test]
    public async Task Build_ASlopeOnTheLeftRunsTowardsNegativeU()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();
        var left = new SideGrading { Enabled = true, SlopeMode = TopoAlign.Grading.SlopeMode.Percent, CutValue = 50, FillValue = 25 };

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles[name], left, null);

        var cut = preview.Slopes.Single(s => s.IsCut);
        await Assert.That(cut.Side).IsEqualTo(AssemblySide.Left);
        await Assert.That(cut.U).IsEqualTo(-6.75).Within(1e-9);
        await Assert.That(cut.Label).IsEqualTo("cut 50%");
        await Assert.That(cut.HeightAt(-8.75)).IsEqualTo(1.5).Within(1e-9);
        await Assert.That(preview.Slopes.Single(s => !s.IsCut).HeightAt(-10.75)).IsEqualTo(-0.5).Within(1e-9);
    }

    [Test]
    public async Task Build_NoGrading_OrAnUnusableSlope_DrawsNoSlopes()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();

        var off = AssemblyPreview.Build(assembly.Parts, name => profiles[name], new SideGrading { Enabled = false }, null);
        var zero = AssemblyPreview.Build(assembly.Parts, name => profiles[name], new SideGrading { Enabled = true, CutValue = 0 }, null);

        await Assert.That(off.Slopes.Count).IsEqualTo(0);
        await Assert.That(zero.Slopes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_RightHandPartsAreAtPositiveU()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles[name]);

        var left = preview.Outlines[1].Points;
        var right = preview.Outlines[2].Points;
        await Assert.That(left.Max(p => p.X)).IsLessThan(0.0);
        await Assert.That(right.Min(p => p.X)).IsGreaterThan(0.0);
    }

    [Test]
    public async Task Build_BoundsCoverEveryPart()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var profiles = AssemblyFixtures.Profiles();

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles[name]);

        // road is 12 wide (-6..6); the kerbs sit 6.25 ft out and are 0.5 wide
        await Assert.That(preview.MinU).IsEqualTo(-6.75).Within(1e-9);
        await Assert.That(preview.MaxU).IsEqualTo(6.75).Within(1e-9);
        await Assert.That(preview.MinV).IsEqualTo(-0.5).Within(1e-9);
        await Assert.That(preview.MaxV).IsEqualTo(0.5).Within(1e-9);
    }

    [Test]
    public async Task Build_AnUnresolvedProfileIsListedAndNotDrawn()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Parts[1].ProfileName = string.Empty;
        var profiles = AssemblyFixtures.Profiles();

        var preview = AssemblyPreview.Build(assembly.Parts, name => profiles.TryGetValue(name, out var p) ? p : null);

        await Assert.That(preview.Outlines.Count).IsEqualTo(2);
        await Assert.That(preview.Missing.Count).IsEqualTo(1);
        await Assert.That(preview.Missing[0]).IsEqualTo("Kerb (left)");
    }

    [Test]
    public async Task Build_NoProfilesAtAll_HasNoOutlines()
    {
        var assembly = AssemblyFixtures.RoadAndKerbs();

        var preview = AssemblyPreview.Build(assembly.Parts, _ => null);

        await Assert.That(preview.HasOutlines).IsFalse();
        await Assert.That(preview.Missing.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Build_AnUnnamedPartIsCalledByItsPosition()
    {
        var parts = new List<AssemblyPart> { new() { Name = " ", ProfileName = string.Empty } };

        var preview = AssemblyPreview.Build(parts, _ => null);

        await Assert.That(preview.Missing[0]).IsEqualTo("Part 1");
    }
}

public class AssemblyStoreTests : TopoAlignTest
{
    private static string NewFolder() => Path.Combine(Path.GetTempPath(), "TopoAlignAssemblies_" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task SaveThenLoad_RoundTripsEveryValue()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var assembly = AssemblyFixtures.RoadAndKerbs();

            store.Save(assembly);
            var loaded = store.LoadAll();

            await Assert.That(loaded.Count).IsEqualTo(1);
            await Assert.That(loaded[0].Name).IsEqualTo("Road with kerbs");
            await Assert.That(loaded[0].Parts.Count).IsEqualTo(3);
            await Assert.That(loaded[0].Parts[2].Name).IsEqualTo("Kerb (right)");
            await Assert.That(loaded[0].Parts[2].ProfileName).IsEqualTo("Kerb");
            await Assert.That(loaded[0].Parts[2].MirrorProfile).IsTrue();
            await Assert.That(loaded[0].Parts[1].MirrorProfile).IsFalse();
            await Assert.That(loaded[0].Parts[2].LateralOffsetMm).IsEqualTo(assembly.Parts[2].LateralOffsetMm).Within(1e-9);
            await Assert.That(loaded[0].Parts[1].LateralOffsetMm).IsEqualTo(assembly.Parts[1].LateralOffsetMm).Within(1e-9);
            await Assert.That(loaded[0].Parts[0].Category).IsEqualTo("OST_GenericModel");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task SaveThenLoad_RoundTripsTheSideGrading()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var assembly = AssemblyFixtures.RoadAndKerbs();
            assembly.Left = new SideGrading { Enabled = true, SlopeMode = TopoAlign.Grading.SlopeMode.Percent, CutValue = 40, FillValue = 25, MaxWidthMm = 12000 };

            store.Save(assembly);
            var loaded = store.LoadAll()[0];

            await Assert.That(File.ReadAllText(store.PathFor(assembly.Name))).Contains("\"Percent\"");
            await Assert.That(loaded.Left.Enabled).IsTrue();
            await Assert.That(loaded.Left.SlopeMode).IsEqualTo(TopoAlign.Grading.SlopeMode.Percent);
            await Assert.That(loaded.Left.CutValue).IsEqualTo(40.0).Within(1e-9);
            await Assert.That(loaded.Left.FillValue).IsEqualTo(25.0).Within(1e-9);
            await Assert.That(loaded.Left.MaxWidthMm).IsEqualTo(12000.0).Within(1e-9);
            await Assert.That(loaded.Right.Enabled).IsFalse();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task LoadAll_AnAssemblyFromBeforeGradingExisted_HasGradingOff()
    {
        var folder = NewFolder();
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "old.json"), "{ \"Name\": \"Old\", \"Parts\": [], \"Left\": null }");

            var loaded = new AssemblyStore(folder).LoadAll();

            await Assert.That(loaded.Count).IsEqualTo(1);
            await Assert.That(loaded[0].Left.Enabled).IsFalse();
            await Assert.That(loaded[0].Right.Enabled).IsFalse();
            await Assert.That(loaded[0].Right.CutValue).IsEqualTo(2.0).Within(1e-9);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task LoadAll_NoFolder_IsEmpty()
    {
        var store = new AssemblyStore(NewFolder());

        await Assert.That(store.LoadAll().Count).IsEqualTo(0);
    }

    [Test]
    public async Task LoadAll_SkipsAFileThatCannotBeRead()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            File.WriteAllText(Path.Combine(folder, "broken.json"), "{ this is not json");
            File.WriteAllText(Path.Combine(folder, "nameless.json"), "{ \"Parts\": [] }");

            var loaded = store.LoadAll();

            await Assert.That(loaded.Count).IsEqualTo(1);
            await Assert.That(loaded[0].Name).IsEqualTo("Road with kerbs");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Save_SavingAgainUnderTheSameNameReplaces()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var assembly = AssemblyFixtures.RoadAndKerbs();
            store.Save(assembly);

            assembly.Parts.RemoveAt(2);
            store.Save(assembly);

            await Assert.That(store.LoadAll().Single().Parts.Count).IsEqualTo(2);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Save_ANameWithPathCharacters_StaysInsideTheFolder()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var assembly = AssemblyFixtures.RoadAndKerbs();
            assembly.Name = "A/B:C*D";

            store.Save(assembly);

            await Assert.That(Directory.GetFiles(folder).Length).IsEqualTo(1);
            await Assert.That(Path.GetDirectoryName(store.PathFor(assembly.Name))).IsEqualTo(folder);
            await Assert.That(store.LoadAll().Single().Name).IsEqualTo("A/B:C*D");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Save_WithoutAName_IsRefused()
    {
        var store = new AssemblyStore(NewFolder());

        var exception = Catch(() => store.Save(new SweepAssembly { Name = "  " }));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Delete_RemovesTheFile_AndIgnoresAMissingOne()
    {
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());

            store.Delete("Road with kerbs");
            store.Delete("Never saved");

            await Assert.That(store.LoadAll().Count).IsEqualTo(0);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}

/// <summary>Finding where an assembly's slope leaves it, and the line the slope starts from. No Revit geometry involved.</summary>
public class AssemblyGraderTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    [Test]
    public async Task Shoulder_IsTheOutermostPointOfTheWholeAssembly_AtItsHighestHeight()
    {
        // a road from u -6 to 6, a kerb outside each side (0.5 wide, 0.5 high) and a footway beyond the right kerb
        var parts = new List<SectionProfile>
        {
            AssemblyFixtures.Box(-6, -0.5, 6, 0),
            AssemblyFixtures.Box(-6.5, 0, -6, 0.5),
            AssemblyFixtures.Box(6, 0, 6.5, 0.5),
            AssemblyFixtures.Box(6.5, 0, 9.5, 0.1)
        };

        await Assert.That(AssemblyGrader.TryFindShoulder(parts, AssemblySide.Left, out var leftU, out var leftV)).IsTrue();
        await Assert.That(AssemblyGrader.TryFindShoulder(parts, AssemblySide.Right, out var rightU, out var rightV)).IsTrue();

        await Assert.That(leftU).IsEqualTo(-6.5).Within(Tol);
        await Assert.That(leftV).IsEqualTo(0.5).Within(Tol);
        await Assert.That(rightU).IsEqualTo(9.5).Within(Tol);
        await Assert.That(rightV).IsEqualTo(0.1).Within(Tol);
    }

    [Test]
    public async Task Shoulder_WithNoParts_IsNotFound()
    {
        await Assert.That(AssemblyGrader.TryFindShoulder(new List<SectionProfile>(), AssemblySide.Left, out _, out _)).IsFalse();
    }

    [Test]
    public async Task EdgePath_FollowsTheAlignmentAtTheShoulderOffsetAndHeight()
    {
        // an alignment running along +X at height 10; the right of travel is -Y
        var stations = new List<double> { 0.0, 10.0, 20.0 };

        var path = AssemblyGrader.BuildEdgePath(
            stations,
            station => new PathPoint(station, 0.0, 10.0),
            _ => (1.0, 0.0),
            6.5,
            0.5,
            0.25,
            AssemblySide.Right,
            out var sideX,
            out var sideY);

        await Assert.That(path.Count).IsEqualTo(3);
        await Assert.That(path[1].X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(path[1].Y).IsEqualTo(-6.5).Within(Tol);
        await Assert.That(path[1].Z).IsEqualTo(10.75).Within(Tol);

        // the side point is beyond the edge, away from the alignment
        await Assert.That(sideX).IsEqualTo(0.0).Within(Tol);
        await Assert.That(sideY).IsEqualTo(-7.5).Within(Tol);
    }

    [Test]
    public async Task EdgePath_OnTheLeft_IsTheOtherSideOfTheAlignment()
    {
        var stations = new List<double> { 0.0, 10.0 };

        var path = AssemblyGrader.BuildEdgePath(
            stations,
            station => new PathPoint(station, 0.0, 0.0),
            _ => (1.0, 0.0),
            -6.5,
            0.0,
            0.0,
            AssemblySide.Left,
            out _,
            out var sideY);

        await Assert.That(path[0].Y).IsEqualTo(6.5).Within(Tol);
        await Assert.That(sideY).IsEqualTo(7.5).Within(Tol);
    }

    [Test]
    public async Task GradedFromTheEdgePath_CutAndFillUseTheirOwnSlopes()
    {
        // ground at height 0 east of the road; one stretch of the edge sits above it (fill), the other below (cut)
        var ground = new TriangleSurface();
        ground.AddTriangle(-100, -100, 0, 100, -100, 0, 100, 100, 0);
        ground.AddTriangle(-100, -100, 0, 100, 100, 0, -100, 100, 0);

        var edge = new List<GradePoint> { new(0, 0, 5), new(0, 10, 5), new(0, 20, -4), new(0, 30, -4) };
        var grading = new SideGrading { CutValue = 2, FillValue = 4 };

        var result = SlopeGrader.Grade(edge, false, 50, 0, grading.CutRiseOverRun(), grading.FillRiseOverRun(), 100, ground, true);

        await Assert.That(result.Stations[0].IsCut).IsFalse();
        await Assert.That(result.Stations[0].Daylight.X).IsEqualTo(20.0).Within(1e-3);
        await Assert.That(result.Stations[3].IsCut).IsTrue();
        await Assert.That(result.Stations[3].Daylight.X).IsEqualTo(8.0).Within(1e-3);
    }

    [Test]
    public async Task SideGrading_ReadsItsSlopesInTheModeChosen()
    {
        var percent = new SideGrading { SlopeMode = TopoAlign.Grading.SlopeMode.Percent, CutValue = 50, FillValue = 25 };
        var ratio = new SideGrading { CutValue = 2, FillValue = 3 };

        await Assert.That(percent.CutRiseOverRun()).IsEqualTo(0.5).Within(Tol);
        await Assert.That(percent.FillRiseOverRun()).IsEqualTo(0.25).Within(Tol);
        await Assert.That(ratio.CutRiseOverRun()).IsEqualTo(0.5).Within(Tol);
        await Assert.That(ratio.FillRiseOverRun()).IsEqualTo(1.0 / 3.0).Within(Tol);
        await Assert.That(new SideGrading { MaxWidthMm = 3048 }.MaxWidthFeet).IsEqualTo(10.0).Within(Tol);
    }
}

public class AssemblyBuilderTests : TopoAlignTest
{
    private static readonly Options FineOptions = new() { DetailLevel = ViewDetailLevel.Fine };

    private static List<Element> Shapes(Document document, Element alignment) => RoadBuilder.FindRoads(document, alignment);

    private static Element Named(Document document, Element alignment, string part)
    {
        return Shapes(document, alignment).Single(e =>
            e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString()?.Contains($" - {part} along ") == true);
    }

    [Test]
    public async Task Create_MakesOneTaggedShapePerPart()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var result = AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(result.BuiltParts.Count).IsEqualTo(3);
        await Assert.That(result.SkippedParts.Count).IsEqualTo(0);
        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(3);
        foreach (var id in result.ShapeIds)
        {
            await Assert.That(RoadBuilder.IsRoad(document.GetElement(id))).IsTrue();
        }

        var comment = Named(document, alignment, "Kerb (right)").get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
        await Assert.That(comment.Contains("Road with kerbs")).IsTrue();
    }

    /// <summary>A flat toposolid at the given height, with a spread of points so it has a surface to read. By default it covers the curved alignment.</summary>
    private static Toposolid FlatTopo(Document document, double z, double minX = -100, double minY = -100, double maxX = 500, double maxY = 400)
    {
        using var transaction = new Transaction(document, "Make topo");
        transaction.Start();

        var level = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
        var type = new FilteredElementCollector(document).OfClass(typeof(ToposolidType)).Cast<ToposolidType>().First();

        var loop = new CurveLoop();
        var corners = new[] { new XYZ(minX, minY, z), new XYZ(maxX, minY, z), new XYZ(maxX, maxY, z), new XYZ(minX, maxY, z) };
        for (var i = 0; i < corners.Length; i++)
        {
            loop.Append(Line.CreateBound(corners[i], corners[(i + 1) % corners.Length]));
        }

        // a grid well inside the boundary
        var points = new List<XYZ>();
        for (var x = minX + 25; x < maxX; x += 50)
        {
            for (var y = minY + 25; y < maxY; y += 50)
            {
                points.Add(new XYZ(x, y, z));
            }
        }

        var topo = Toposolid.Create(document, new List<CurveLoop> { loop }, points, type.Id, level.Id);
        transaction.Commit();
        return topo;
    }

    [Test]
    public async Task Create_WithATopoAndGradingOn_GradesBothSidesAndKeepsTheRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var topo = FlatTopo(document, -3.0);
        var before = topo.GetSlabShapeEditor().SlabShapeVertices.Size;

        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Left = new SideGrading { Enabled = true, CutValue = 2, FillValue = 3 };
        assembly.Right = new SideGrading { Enabled = true, CutValue = 2, FillValue = 3 };

        var result = AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { topo });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(result.Grading.Count).IsEqualTo(2);
        foreach (var side in result.Grading)
        {
            await Assert.That(side.Succeeded).IsTrue();
            // the kerbs stand about 0.5 ft above the road, which is above the ground at -3, so the slopes fall
            await Assert.That(side.Fill).IsGreaterThan(0);
            await Assert.That(side.Cut).IsEqualTo(0);
        }

        await Assert.That(topo.GetSlabShapeEditor().SlabShapeVertices.Size).IsGreaterThan(before);
    }

    [Test]
    public async Task Create_WithADifferentTopoEitherSide_GradesEachSideOnTheTopoItReaches()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        // the alignment runs east along y = 0 first: the left of travel is north of it and the right is south
        var south = FlatTopo(document, -3.0, -100, -100, 200, -2);
        var north = FlatTopo(document, -6.0, -100, 2, 200, 100);
        var southBefore = south.GetSlabShapeEditor().SlabShapeVertices.Size;
        var northBefore = north.GetSlabShapeEditor().SlabShapeVertices.Size;

        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Left = new SideGrading { Enabled = true, CutValue = 2, FillValue = 3 };
        assembly.Right = new SideGrading { Enabled = true, CutValue = 2, FillValue = 3 };

        // the order they are picked in doesn't matter
        var result = AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { north, south });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(result.Grading.All(g => g.Succeeded)).IsTrue();

        var left = result.Grading.Where(g => g.Side == AssemblySide.Left).ToList();
        var right = result.Grading.Where(g => g.Side == AssemblySide.Right).ToList();
        await Assert.That(left.Count).IsEqualTo(1);
        await Assert.That(left[0].ToposolidName).Contains($"[{north.Id}]");
        await Assert.That(right.Count).IsEqualTo(1);
        await Assert.That(right[0].ToposolidName).Contains($"[{south.Id}]");

        await Assert.That(north.GetSlabShapeEditor().SlabShapeVertices.Size).IsGreaterThan(northBefore);
        await Assert.That(south.GetSlabShapeEditor().SlabShapeVertices.Size).IsGreaterThan(southBefore);
    }

    [Test]
    public async Task Create_WhenAPickedToposolidIsReplacedByAnother_StillGradesTheOthers()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        // making a toposolid inside another can make Revit replace one of them, which leaves a stale element in the pick
        var big = FlatTopo(document, -3.0);
        var inner = FlatTopo(document, -3.0, -20, -20, 220, 20);

        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Left = new SideGrading { Enabled = true };
        assembly.Right = new SideGrading { Enabled = true };

        var result = AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { big, inner });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(result.Grading.Any(g => g.Succeeded)).IsTrue();
    }

    [Test]
    public async Task Create_WhenASideReachesNoneOfTheToposolids_ReportsThatSideAsNotGraded()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        // only the south side of the road has any ground
        var south = FlatTopo(document, -3.0, -100, -100, 200, -2);

        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Left = new SideGrading { Enabled = true };
        assembly.Right = new SideGrading { Enabled = true };

        var result = AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { south });

        var left = result.Grading.Single(g => g.Side == AssemblySide.Left);
        var right = result.Grading.Single(g => g.Side == AssemblySide.Right);
        await Assert.That(right.Succeeded).IsTrue();
        await Assert.That(left.Succeeded).IsFalse();
        await Assert.That(left.Error).Contains("does not meet");
    }

    [Test]
    public async Task Create_WithATopoButNoSideGradingOn_LeavesTheGroundAlone()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var topo = FlatTopo(document, -3.0);
        var before = topo.GetSlabShapeEditor().SlabShapeVertices.Size;

        var result = AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { topo });

        await Assert.That(result.Grading.Count).IsEqualTo(0);
        await Assert.That(topo.GetSlabShapeEditor().SlabShapeVertices.Size).IsEqualTo(before);
    }

    [Test]
    public async Task Create_WithGradingOnButNotAToposolid_SaysSoAndStillMakesTheRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Left = new SideGrading { Enabled = true };

        var result = AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 }, new Element[] { alignment });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(result.Grading.Count).IsEqualTo(0);
        await Assert.That(result.GradingNotice).Contains("not a toposolid");
    }

    [Test]
    public async Task Create_ThePartOnTheRoadCentrelineMatchesASingleProfileSweep()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var options = new RoadOptions { Interval = 20.0 };

        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), options);
        var single = RoadFixtures.Volume(Shapes(document, alignment).Single());

        AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), options);
        var road = RoadFixtures.Volume(Named(document, alignment, "Road"));

        await Assert.That(road).IsEqualTo(single).Within(single * 1e-6);
    }

    [Test]
    public async Task Create_APartIsOnTheSideItsOffsetSays()
    {
        // the alignment starts at the origin heading east, so the right of the road is -Y and the left is +Y
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 });

        var right = Named(document, alignment, "Kerb (right)").get_BoundingBox(null);
        var left = Named(document, alignment, "Kerb (left)").get_BoundingBox(null);
        await Assert.That(right.Min.Y).IsLessThan(-5.5);
        await Assert.That(left.Min.Y).IsGreaterThan(5.0);
    }

    [Test]
    public async Task Create_ThePartOutsideTheCurveIsLongerThanTheOneInside()
    {
        // the alignment turns left, so the right-hand kerb is on the outside of the curve and has more volume
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), new RoadOptions { Interval = 10.0 });

        var outside = RoadFixtures.Volume(Named(document, alignment, "Kerb (right)"));
        var inside = RoadFixtures.Volume(Named(document, alignment, "Kerb (left)"));
        await Assert.That(outside).IsGreaterThan(inside);
    }

    [Test]
    public async Task Create_APartIsRaisedByItsVerticalOffset()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var assembly = AssemblyFixtures.RoadAndKerbs();
        assembly.Parts[1].VerticalOffsetMm = 2.0 * AssemblyPart.MillimetresPerFoot;

        AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 });

        var raised = Named(document, alignment, "Kerb (left)").get_BoundingBox(null);
        var level = Named(document, alignment, "Kerb (right)").get_BoundingBox(null);
        await Assert.That(raised.Min.Z - level.Min.Z).IsEqualTo(2.0).Within(1e-6);
    }

    [Test]
    public async Task Create_EachPartIsFiledUnderItsOwnCategory()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var assembly = AssemblyFixtures.RoadAndKerbs();
        var other = RoadBuilder.GetCategories(document).First().Category;
        assembly.Parts[0].Category = other.ToString();
        assembly.Parts[1].Category = "OST_NotACategory";

        AssemblyBuilder.Create(document, alignment, assembly, AssemblyFixtures.Profiles(), new RoadOptions { Interval = 20.0 });

        await Assert.That(Named(document, alignment, "Road").Category.BuiltInCategory).IsEqualTo(other);
        await Assert.That(Named(document, alignment, "Kerb (left)").Category.BuiltInCategory).IsEqualTo(BuiltInCategory.OST_GenericModel);
    }

    [Test]
    public async Task Create_AgainReplacesTheEarlierParts()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var options = new RoadOptions { Interval = 20.0 };

        AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), options);
        var second = AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), options);

        await Assert.That(second.ReplacedPrevious).IsTrue();
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(3);
    }

    [Test]
    public async Task Create_AnAssemblyReplacesASingleProfileRoad_AndTheOtherWayRound()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var options = new RoadOptions { Interval = 20.0 };

        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), options);
        AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), options);
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(3);

        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), options);
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_APartWithoutItsProfileIsSkippedAndNamed()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var profiles = AssemblyFixtures.Profiles();
        profiles.Remove("Kerb");

        var result = AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), profiles, new RoadOptions { Interval = 20.0 });

        await Assert.That(result.ShapeIds.Count).IsEqualTo(1);
        await Assert.That(result.SkippedParts.Count).IsEqualTo(2);
        await Assert.That(result.SkippedParts[0].Contains("Kerb (left)")).IsTrue();
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_WhenNoPartCanBeBuilt_ThrowsAndLeavesTheModelAlone()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var options = new RoadOptions { Interval = 20.0 };
        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), options);

        var exception = Catch(() => AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), new Dictionary<string, SectionProfile>(), options));

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(Shapes(document, alignment).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_SmoothSurfacesWorkForOffsetParts()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var options = new RoadOptions { Interval = 20.0, GeometryMethod = RoadGeometryMethod.SmoothSurfaces };

        var result = AssemblyBuilder.Create(document, alignment, AssemblyFixtures.RoadAndKerbs(), AssemblyFixtures.Profiles(), options);

        await Assert.That(result.ShapeIds.Count).IsEqualTo(3);
        await Assert.That(RoadFixtures.Volume(Named(document, alignment, "Kerb (right)"))).IsGreaterThan(0.0);
    }

    [Test]
    public async Task Create_NoParts_IsRefused()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var exception = Catch(() => AssemblyBuilder.Create(document, alignment, new SweepAssembly { Name = "Empty" }, AssemblyFixtures.Profiles(), new RoadOptions()));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task ParseCategory_UnknownOrBlank_IsGenericModels()
    {
        await Assert.That(AssemblyBuilder.ParseCategory("OST_Roads")).IsEqualTo(BuiltInCategory.OST_Roads);
        await Assert.That(AssemblyBuilder.ParseCategory("")).IsEqualTo(BuiltInCategory.OST_GenericModel);
        await Assert.That(AssemblyBuilder.ParseCategory("nonsense")).IsEqualTo(BuiltInCategory.OST_GenericModel);
    }
}

public class AssemblyViewModelTests : TopoAlignUiTest
{
    private Document _previousDocument;

    [Before(Test)]
    public void RememberAppDocument() => _previousDocument = App.RevitDocument;

    [After(Test)]
    public void RestoreAppDocument() => App.RevitDocument = _previousDocument;

    private static string NewFolder() => Path.Combine(Path.GetTempPath(), "TopoAlignAssemblies_" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task SweepDialog_AssemblyModeNeedsAnAssemblyWithParts()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var document = NewDocument();
            App.RevitDocument = document;
            var alignment = RoadFixtures.CurvedAlignment(document, "Road 1");
            var store = new AssemblyStore(folder);

            var viewModel = new SweepAlignmentViewModel(store);
            viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
            viewModel.SelectAlignment(alignment);
            viewModel.SelectedMode = viewModel.Modes.First(m => m.Value == SweepMode.Assembly);

            // nothing saved yet
            await Assert.That(viewModel.IsAssemblyMode).IsTrue();
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();
            await Assert.That(viewModel.CreateButtonText).IsEqualTo("Create Road Assembly");
            await Assert.That(viewModel.ShowCategory).IsFalse();

            // save one through the editor, as the user would; the profile types aren't loaded in a new project, so they are reported as missing
            var editor = new AssemblyEditorViewModel(new ProfileLibrary(document), store, viewModel.Categories, viewModel.DisplayUnits, viewModel.SelectedDisplayUnit, null);
            editor.AssemblyName = "Footpath";
            editor.Parts[0].ProfileName = "Paving";
            editor.SaveCommand.Execute(null);
            viewModel.ApplyEditor(editor);

            await Assert.That(viewModel.SelectedAssembly?.Name).IsEqualTo("Footpath");
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();
            await Assert.That(viewModel.AssemblyStatus.Contains("missing")).IsTrue();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task SweepDialog_RemembersTheModeAndAssembly()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var document = NewDocument();
            App.RevitDocument = document;
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var settings = new Settings { SweepMode = "Assembly", SweepAssemblyName = "Road with kerbs" };
            settings.SaveSettings();

            var viewModel = new SweepAlignmentViewModel(store);

            await Assert.That(viewModel.IsAssemblyMode).IsTrue();
            await Assert.That(viewModel.SelectedAssembly?.Name).IsEqualTo("Road with kerbs");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private AssemblyEditorViewModel NewEditor(AssemblyStore store, string name = null)
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var units = new SweepAlignmentViewModel(store).DisplayUnits;
        var feet = units.First(u => u.DisplayUnit == UnitTypeId.Feet);
        return new AssemblyEditorViewModel(new ProfileLibrary(document), store, RoadBuilder.GetCategories(document), units, feet, name);
    }

    [Test]
    public async Task Editor_StartsWithOneEmptyPartWhenNothingIsSaved()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var editor = NewEditor(new AssemblyStore(folder));

            await Assert.That(editor.Parts.Count).IsEqualTo(1);
            await Assert.That(editor.SaveCommand.CanExecute(null)).IsFalse();
            await Assert.That(editor.Preview.HasOutlines).IsFalse();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_ShowsSavedValuesInTheDisplayUnit_AndSavesThemInMillimetres()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());

            var editor = NewEditor(store, "Road with kerbs");

            await Assert.That(editor.Parts.Count).IsEqualTo(3);
            await Assert.That(editor.Parts[2].LateralOffset).IsEqualTo(6.25m);
            await Assert.That(editor.Parts[2].MirrorProfile).IsTrue();
            // the profile types aren't loaded in a new project; the saved name is kept so saving doesn't lose it
            await Assert.That(editor.Parts[2].ProfileName).IsEqualTo("Kerb");

            editor.Parts[2].LateralOffset = 7m;
            editor.SaveCommand.Execute(null);

            var saved = store.LoadAll().Single();
            await Assert.That(saved.Parts[2].LateralOffsetMm).IsEqualTo(7.0 * AssemblyPart.MillimetresPerFoot).Within(0.01);
            await Assert.That(saved.Parts[2].ProfileName).IsEqualTo("Kerb");
            await Assert.That(editor.Changed).IsTrue();
            await Assert.That(editor.SavedName).IsEqualTo("Road with kerbs");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Views_LoadWithTheGradingControls()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var editor = NewEditor(store, "Road with kerbs");
            var viewModel = new SweepAlignmentViewModel(store);
            object sweepContext = null;
            object editorContext = null;

            RunOnStaThread(() =>
            {
                sweepContext = new TopoAlign.Views.SweepAlignmentView(viewModel).DataContext;
                editorContext = new TopoAlign.Views.AssemblyEditorView(editor).DataContext;
            });

            await Assert.That(sweepContext).IsEqualTo(viewModel);
            await Assert.That(editorContext).IsEqualTo(editor);
            await Assert.That(editor.CanGradeTopo).IsTrue();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task SweepDialog_GradingTheTopoNeedsAToposolidPicked()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var document = NewDocument();
            App.RevitDocument = document;
            var alignment = RoadFixtures.CurvedAlignment(document, "Road 1");
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());

            var viewModel = new SweepAlignmentViewModel(store);
            viewModel.SelectAlignment(alignment);

            // single-profile mode never shows the grading controls
            await Assert.That(viewModel.ShowGradeTopo).IsFalse();

            viewModel.SelectedMode = viewModel.Modes.First(m => m.Value == SweepMode.Assembly);
            await Assert.That(viewModel.ShowGradeTopo).IsTrue();
            await Assert.That(viewModel.ShowGradeTopoPick).IsFalse();
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();

            viewModel.GradeTopo = true;
            await Assert.That(viewModel.ShowGradeTopoPick).IsTrue();
            await Assert.That(viewModel.GradeTopoStatus).Contains("No toposolid picked");
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();

            var first = FlatTopoFor(document);
            viewModel.SelectToposolid(first);
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();
            await Assert.That(viewModel.Topographies.Count).IsEqualTo(1);

            // several can be picked, but the same one isn't added twice
            viewModel.SelectToposolid(first);
            viewModel.SelectToposolid(FlatTopoFor(document));
            await Assert.That(viewModel.Topographies.Count).IsEqualTo(2);
            await Assert.That(viewModel.GradeTopoStatus).Contains("Toposolids (2)");

            // picking again replaces the choice
            viewModel.SelectToposolids(new Element[] { first });
            await Assert.That(viewModel.Topographies.Count).IsEqualTo(1);
            await Assert.That(viewModel.GradeTopoStatus).Contains("Toposolid:");
            // the assembly has no side grading turned on, which the status says
            await Assert.That(viewModel.GradeTopoStatus).Contains("no side grading");

            // unticking it puts the dialog back to how it was, whatever was picked
            viewModel.GradeTopo = false;
            await Assert.That(viewModel.GradeTopoStatus).IsEqualTo(string.Empty);
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    private static Toposolid FlatTopoFor(Document document)
    {
        using var transaction = new Transaction(document, "Make topo");
        transaction.Start();

        var level = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
        var type = new FilteredElementCollector(document).OfClass(typeof(ToposolidType)).Cast<ToposolidType>().First();
        var loop = new CurveLoop();
        var corners = new[] { new XYZ(0, 0, 0), new XYZ(10, 0, 0), new XYZ(10, 10, 0), new XYZ(0, 10, 0) };
        for (var i = 0; i < corners.Length; i++)
        {
            loop.Append(Line.CreateBound(corners[i], corners[(i + 1) % corners.Length]));
        }

        var topo = Toposolid.Create(document, new List<CurveLoop> { loop }, new List<XYZ> { new XYZ(5, 5, 0) }, type.Id, level.Id);
        transaction.Commit();
        return topo;
    }

    [Test]
    public async Task Editor_SavesTheSideGrading_AndShowsItAgain()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var assembly = AssemblyFixtures.RoadAndKerbs();
            assembly.Right = new SideGrading { Enabled = true, CutValue = 2, FillValue = 4, MaxWidthMm = 10 * AssemblyPart.MillimetresPerFoot };
            store.Save(assembly);

            var editor = NewEditor(store, "Road with kerbs");

            await Assert.That(editor.RightGrading.Enabled).IsTrue();
            await Assert.That(editor.RightGrading.FillValue).IsEqualTo(4m);
            await Assert.That(editor.RightGrading.MaxWidth).IsEqualTo(10m);
            await Assert.That(editor.LeftGrading.Enabled).IsFalse();

            // the picture follows the grading as it is edited (the fixture's profile types aren't loaded, so there is no outline to start from yet)
            var changes = 0;
            editor.PreviewChanged += (_, _) => changes++;
            editor.RightGrading.CutValue = 3m;
            await Assert.That(changes).IsGreaterThan(0);

            editor.LeftGrading.Enabled = true;
            editor.LeftGrading.CutValue = 1.5m;
            editor.SaveCommand.Execute(null);

            var saved = store.LoadAll().Single();
            await Assert.That(saved.Left.Enabled).IsTrue();
            await Assert.That(saved.Left.CutValue).IsEqualTo(1.5).Within(1e-9);
            await Assert.That(saved.Right.MaxWidthMm).IsEqualTo(10 * AssemblyPart.MillimetresPerFoot).Within(0.01);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_RefusesToSaveAGradingSlopeOfZero()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var editor = NewEditor(store, "Road with kerbs");

            editor.LeftGrading.Enabled = true;
            editor.LeftGrading.FillValue = 0m;
            editor.SaveCommand.Execute(null);

            await Assert.That(editor.Status).Contains("Left grading");
            await Assert.That(store.LoadAll().Single().Left.Enabled).IsFalse();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_AddRemoveAndMoveParts()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var editor = NewEditor(store, "Road with kerbs");

            editor.SelectedPart = editor.Parts[2];
            editor.MoveUpCommand.Execute(null);
            await Assert.That(editor.Parts[1].Name).IsEqualTo("Kerb (right)");
            await Assert.That(editor.SelectedPart).IsEqualTo(editor.Parts[1]);

            editor.MoveDownCommand.Execute(null);
            await Assert.That(editor.Parts[2].Name).IsEqualTo("Kerb (right)");

            editor.RemovePartCommand.Execute(null);
            await Assert.That(editor.Parts.Count).IsEqualTo(2);

            editor.AddPartCommand.Execute(null);
            await Assert.That(editor.Parts.Count).IsEqualTo(3);
            await Assert.That(editor.SelectedPart).IsEqualTo(editor.Parts[2]);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_WillNotSaveAPartWithoutAProfile()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            var editor = NewEditor(store);
            editor.AssemblyName = "No profile";

            editor.SaveCommand.Execute(null);

            await Assert.That(store.LoadAll().Count).IsEqualTo(0);
            await Assert.That(editor.Status.Contains("Choose a profile")).IsTrue();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_DeleteRemovesTheSavedAssembly()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var editor = NewEditor(store, "Road with kerbs");

            editor.DeleteAssemblyCommand.Execute(null);

            await Assert.That(store.LoadAll().Count).IsEqualTo(0);
            await Assert.That(editor.AssemblyNames.Count).IsEqualTo(0);
            await Assert.That(editor.Changed).IsTrue();
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    [Test]
    public async Task Editor_ChangingUnitsConvertsTheOffsets_ButNotTheSavedMillimetres()
    {
        using var backup = new SettingsBackup();
        var folder = NewFolder();
        try
        {
            var store = new AssemblyStore(folder);
            store.Save(AssemblyFixtures.RoadAndKerbs());
            var editor = NewEditor(store, "Road with kerbs");
            var before = editor.ToAssembly().Parts[2].LateralOffsetMm;

            editor.SelectedDisplayUnit = editor.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Millimeters);

            await Assert.That(Math.Abs(editor.Parts[2].LateralOffset - 1905m) < 0.01m).IsTrue();
            await Assert.That(editor.ToAssembly().Parts[2].LateralOffsetMm).IsEqualTo(before).Within(0.01);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
