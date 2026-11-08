using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.IO;
using TopoAlign.Alignments;
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
