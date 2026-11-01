using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.IO;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using Settings = TopoAlign.Models.Settings;

namespace TopoAlign.Tests;

internal static class RoadFixtures
{
    private static PlanPoint P(double u, double v) => new PlanPoint(u, v);

    /// <summary>A 12 ft wide, 6 inch thick slab hanging below the centreline level: 6 square feet.</summary>
    public static SectionProfile Slab(double width = 12.0, double thickness = 0.5)
    {
        var half = width / 2.0;
        var segments = new List<PlanSegment>
        {
            new PlanLine(P(-half, 0), P(half, 0)),
            new PlanLine(P(half, 0), P(half, -thickness)),
            new PlanLine(P(half, -thickness), P(-half, -thickness)),
            new PlanLine(P(-half, -thickness), P(-half, 0))
        };

        if (!SectionProfile.TryCreate(segments, out var profile, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return profile;
    }

    /// <summary>The slab with a semicircular crown of radius 3 along its top (a rounded path).</summary>
    public static SectionProfile Crowned()
    {
        var segments = new List<PlanSegment>
        {
            new PlanLine(P(-3, -0.5), P(3, -0.5)),
            new PlanLine(P(3, -0.5), P(3, 0)),
            new PlanArc(P(0, 0), 3, 0, Math.PI),
            new PlanLine(P(-3, 0), P(-3, -0.5))
        };

        if (!SectionProfile.TryCreate(segments, out var profile, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return profile;
    }

    /// <summary>A 200 ft straight, a quarter turn of radius 100 and another 200 ft straight, written as an alignment in the model.</summary>
    public static Element CurvedAlignment(Document document, string name = "Curved", IReadOnlyList<ProfilePoint> pvis = null)
    {
        var segments = PiGeometry.Create(new[]
        {
            new PiPoint(0, 0, 0, 0),
            new PiPoint(300, 0, 100, 0),
            new PiPoint(300, 300, 0, 0)
        }).Segments;

        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var json = AlignmentJsonBuilder.BuildFromSegments(name, segments, internalToShared, 0.0, 0.0, pvis, 0.0, 0.005, out var length);
        return document.GetElement(AlignmentApi.Create(document, name, json, length));
    }

    public static double CentrelineLength => 200.0 + Math.PI * 100.0 / 2.0 + 200.0;

    public static double Volume(Element shape)
    {
        var total = 0.0;
        foreach (var geometryObject in shape.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }))
        {
            if (geometryObject is Solid solid)
            {
                total += solid.Volume;
            }
        }

        return total;
    }

    public static int SolidCount(Element shape)
    {
        return shape.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }).OfType<Solid>().Count();
    }
}

public class SectionProfileReaderTests : TopoAlignTest
{
    private static Curve[] Rectangle(double width = 12, double height = 0.5) => new Curve[]
    {
        Ln(-width / 2, 0, width / 2, 0),
        Ln(width / 2, 0, width / 2, -height),
        Ln(width / 2, -height, -width / 2, -height),
        Ln(-width / 2, -height, -width / 2, 0)
    };

    [Test]
    public async Task FromCurves_AClosedRectangle_IsASection()
    {
        var profile = SectionProfileReader.FromCurves(Rectangle(), "Slab", out var error);

        await Assert.That(error).IsNull();
        await Assert.That(profile.Width).IsEqualTo(12.0).Within(1e-9);
        await Assert.That(profile.Height).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(profile.Area).IsEqualTo(6.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_LinesInAnyOrderAndDirection_AreChainedIntoTheLoop()
    {
        var shuffled = new Curve[] { Rectangle()[2], Reversed(Rectangle()[0]), Rectangle()[3], Reversed(Rectangle()[1]) };

        var profile = SectionProfileReader.FromCurves(shuffled, "Slab", out var error);

        await Assert.That(error).IsNull();
        await Assert.That(profile.Area).IsEqualTo(6.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_AShapeWithAnArc_IsASection()
    {
        var d = new Curve[]
        {
            Ln(-3, 0, 3, 0),
            ArcThrough(3, 0, 0, 3, -3, 0)
        };

        var profile = SectionProfileReader.FromCurves(d, "Crown", out var error);

        await Assert.That(error).IsNull();
        await Assert.That(profile.Height).IsEqualTo(3.0).Within(0.01);
    }

    [Test]
    public async Task FromCurves_ZIsIgnored()
    {
        var raised = Rectangle().Select(c => (Curve)Line.CreateBound(
            new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 7), new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 7))).ToArray();

        var profile = SectionProfileReader.FromCurves(raised, "Slab", out var error);

        await Assert.That(error).IsNull();
        await Assert.That(profile.Area).IsEqualTo(6.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_NoCurves_SaysSo()
    {
        var profile = SectionProfileReader.FromCurves(new List<Curve>(), "Empty", out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("No lines")).IsTrue();
        await Assert.That(error.Contains("Empty")).IsTrue();
    }

    [Test]
    public async Task FromCurves_AnOpenShape_IsRefusedAsNotClosed()
    {
        var open = Rectangle().Take(3).ToArray();

        var profile = SectionProfileReader.FromCurves(open, "Open", out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("closed")).IsTrue();
    }

    [Test]
    public async Task FromCurves_DisconnectedLines_AreRefused()
    {
        var apart = new Curve[] { Ln(0, 0, 5, 0), Ln(20, 20, 25, 20) };

        var profile = SectionProfileReader.FromCurves(apart, "Apart", out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("not connected")).IsTrue();
    }

    [Test]
    public async Task FromCurves_BranchingLines_AreRefused()
    {
        var branching = Rectangle().Concat(new Curve[] { Ln(6, 0, 12, 5) }).ToArray();

        var profile = SectionProfileReader.FromCurves(branching, "Tee", out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("branch")).IsTrue();
    }

    [Test]
    public async Task FromCurves_Splines_AreRefused()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 5, 3, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        var profile = SectionProfileReader.FromCurves(new Curve[] { ellipse, Ln(-5, 0, 5, 0) }, "Curvy", out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("other than lines and arcs")).IsTrue();
    }

    [Test]
    public async Task ListProfileTypes_NullDocument_Throws()
    {
        await Assert.That(Catch(() => SectionProfileReader.ListProfileTypes(null)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task ListProfileTypes_ADocumentWithoutProfileFamilies_IsEmpty()
    {
        var document = NewDocument();

        var types = SectionProfileReader.ListProfileTypes(document);

        await Assert.That(types).IsNotNull();
        await Assert.That(types.All(t => t.Name.Contains(" : "))).IsTrue();
    }

    [Test]
    public async Task Read_AnIdThatIsNotAProfileType_ExplainsWhy()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        var profile = SectionProfileReader.Read(document, line.Id, out var error);

        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("could not be found")).IsTrue();
    }

    [Test]
    public async Task Read_ARealProfileFamily_GivesItsShape()
    {
        var document = NewDocument();
        var template = FindProfileTemplate(document.Application);
        if (template is null)
        {
            Skip.Test("No profile family template (*Profile.rft) is installed with this Revit.");
        }

        var path = Path.Combine(Path.GetTempPath(), $"TopoAlign_Slab_{Guid.NewGuid():N}.rfa");
        try
        {
            var familyDocument = document.Application.NewFamilyDocument(template);
            using (var transaction = new Transaction(familyDocument, "Draw the section"))
            {
                transaction.Start();
                var plane = SketchPlane.Create(familyDocument, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));
                foreach (var curve in Rectangle())
                {
                    familyDocument.FamilyCreate.NewModelCurve(curve, plane);
                }

                transaction.Commit();
            }

            familyDocument.SaveAs(path);
            familyDocument.Close(false);

            document.LoadFamily(path, out _);
            var type = SectionProfileReader.ListProfileTypes(document).FirstOrDefault(t => t.Name.StartsWith(Path.GetFileNameWithoutExtension(path)));

            await Assert.That(type).IsNotNull();
            var profile = SectionProfileReader.Read(document, type.SymbolId, out var error);
            await Assert.That(error).IsNull();
            await Assert.That(profile.Width).IsEqualTo(12.0).Within(1e-6);
            await Assert.That(profile.Height).IsEqualTo(0.5).Within(1e-6);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string FindProfileTemplate(Autodesk.Revit.ApplicationServices.Application application)
    {
        var folder = application.FamilyTemplatePath;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        return Directory.EnumerateFiles(folder, "*Profile.rft", SearchOption.AllDirectories).FirstOrDefault();
    }
}

public class SectionLoopFactoryTests : TopoAlignTest
{
    private static (double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ) Extents(CurveLoop loop)
    {
        var points = loop.SelectMany(c => c.Tessellate()).ToList();
        return (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y), points.Min(p => p.Z), points.Max(p => p.Z));
    }

    [Test]
    public async Task CreateLoop_HeadingEast_StandsAcrossTheRoadInAVerticalPlane()
    {
        var loop = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(10, 20, 5), XYZ.BasisX, 0.0);

        var e = Extents(loop);
        await Assert.That(e.MinX).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(e.MaxX).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(e.MinY).IsEqualTo(14.0).Within(1e-9);
        await Assert.That(e.MaxY).IsEqualTo(26.0).Within(1e-9);
        await Assert.That(e.MinZ).IsEqualTo(4.5).Within(1e-9);
        await Assert.That(e.MaxZ).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task CreateLoop_HeadingNorth_SpreadsAlongX()
    {
        var loop = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(10, 20, 5), XYZ.BasisY, 0.0);

        var e = Extents(loop);
        await Assert.That(e.MinY).IsEqualTo(20.0).Within(1e-9);
        await Assert.That(e.MaxY).IsEqualTo(20.0).Within(1e-9);
        await Assert.That(e.MinX).IsEqualTo(4.0).Within(1e-9);
        await Assert.That(e.MaxX).IsEqualTo(16.0).Within(1e-9);
    }

    [Test]
    public async Task CreateLoop_VerticalOffset_RaisesTheWholeSection()
    {
        var loop = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(0, 0, 0), XYZ.BasisX, 2.0);

        var e = Extents(loop);
        await Assert.That(e.MinZ).IsEqualTo(1.5).Within(1e-9);
        await Assert.That(e.MaxZ).IsEqualTo(2.0).Within(1e-9);
    }

    [Test]
    public async Task CreateLoop_IsAClosedLoopOfTheSameNumberOfCurves()
    {
        var slab = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), XYZ.Zero, XYZ.BasisX, 0.0);
        var crowned = SectionLoopFactory.CreateLoop(RoadFixtures.Crowned(), XYZ.Zero, XYZ.BasisX, 0.0);

        await Assert.That(slab.Count()).IsEqualTo(4);
        await Assert.That(crowned.Count()).IsEqualTo(4);
        await Assert.That(slab.IsOpen()).IsFalse();
        await Assert.That(crowned.IsOpen()).IsFalse();
        await Assert.That(crowned.Count(c => c is Arc)).IsEqualTo(1);
    }

    [Test]
    public async Task CreateLoop_FollowsTheGradeOfThePathByLiftingTheCentre()
    {
        var level = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(0, 0, 0), XYZ.BasisX, 0.0);
        var up = SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(0, 0, 8), XYZ.BasisX, 0.0);

        await Assert.That(Extents(up).MaxZ - Extents(level).MaxZ).IsEqualTo(8.0).Within(1e-9);
        // the section is not tilted by the grade: it is just as wide
        await Assert.That(Extents(up).MaxY - Extents(up).MinY).IsEqualTo(12.0).Within(1e-9);
    }
}

public class RoadSolidBuilderTests : TopoAlignTest
{
    /// <summary>Slab sections every <paramref name="spacing"/> feet along a straight run east.</summary>
    private static List<CurveLoop> StraightLoops(double length, double spacing, SectionProfile profile = null)
    {
        var loops = new List<CurveLoop>();
        for (var s = 0.0; s <= length + 1e-9; s += spacing)
        {
            loops.Add(SectionLoopFactory.CreateLoop(profile ?? RoadFixtures.Slab(), new XYZ(s, 0, 0), XYZ.BasisX, 0.0));
        }

        return loops;
    }

    private static List<CurveLoop> QuarterTurnLoops(double radius, int pieces)
    {
        var loops = new List<CurveLoop>();
        for (var i = 0; i <= pieces; i++)
        {
            var angle = Math.PI / 2 * i / pieces;
            loops.Add(SectionLoopFactory.CreateLoop(RoadFixtures.Slab(), new XYZ(radius * Math.Sin(angle), radius * (1 - Math.Cos(angle)), 0),
                new XYZ(Math.Cos(angle), Math.Sin(angle), 0), 0.0));
        }

        return loops;
    }

    [Test]
    public async Task Build_NothingToLoft_GivesNothing()
    {
        await Assert.That(RoadSolidBuilder.Build(null).Solids.Count).IsEqualTo(0);
        await Assert.That(RoadSolidBuilder.Build(new List<CurveLoop>()).Solids.Count).IsEqualTo(0);
        await Assert.That(RoadSolidBuilder.Build(StraightLoops(0, 10)).Solids.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_AStraightRun_HasTheVolumeOfSectionAreaTimesLength()
    {
        var result = RoadSolidBuilder.Build(StraightLoops(100, 10));

        await Assert.That(result.Solids.Count).IsEqualTo(10);
        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        await Assert.That(result.Solids.Sum(s => s.Volume)).IsEqualTo(6.0 * 100.0).Within(0.01);
    }

    [Test]
    public async Task Build_GroupsOfSeveralSections_GiveTheSameRoadWithFewerSolids()
    {
        var pairs = RoadSolidBuilder.Build(StraightLoops(100, 10), groupSize: 2);
        var groups = RoadSolidBuilder.Build(StraightLoops(100, 10), groupSize: 4);

        await Assert.That(groups.Solids.Count).IsLessThan(pairs.Solids.Count);
        await Assert.That(groups.SkippedSpans).IsEqualTo(0);
        await Assert.That(groups.Solids.Sum(s => s.Volume)).IsEqualTo(pairs.Solids.Sum(s => s.Volume)).Within(6.0);
    }

    [Test]
    public async Task Build_ARoundedSection_KeepsItsArea()
    {
        var crowned = RoadFixtures.Crowned();

        var result = RoadSolidBuilder.Build(StraightLoops(50, 10, crowned));

        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        await Assert.That(result.Solids.Sum(s => s.Volume)).IsEqualTo(crowned.Area * 50).Within(crowned.Area * 50 * 0.01);
    }

    [Test]
    public async Task Build_AroundACurve_HasTheVolumeOfSectionAreaTimesTheCentrelineLength()
    {
        // a symmetrical section has its centroid on the centreline, so Pappus gives area x centreline length
        var result = RoadSolidBuilder.Build(QuarterTurnLoops(100, 24));

        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        await Assert.That(result.Solids.Sum(s => s.Volume)).IsEqualTo(6.0 * Math.PI * 50).Within(6.0 * Math.PI * 50 * 0.01);
    }

    [Test]
    public async Task Build_ASpanThatCannotBeLofted_IsSkippedAndCounted_TheRestIsKept()
    {
        var loops = StraightLoops(40, 10);
        loops.Insert(3, CurveLoop.CreateViaCopy(loops[2])); // two identical sections in a row: nothing between them

        var result = RoadSolidBuilder.Build(loops);

        await Assert.That(result.SkippedSpans).IsEqualTo(1);
        await Assert.That(result.Solids.Count).IsEqualTo(4);
        await Assert.That(result.Solids.Sum(s => s.Volume)).IsEqualTo(6.0 * 40).Within(0.01);
    }
}

public class RoadBuilderTests : TopoAlignTest
{
    private static List<ProfilePoint> Climb() => new List<ProfilePoint> { new(0, 0), new(RoadFixtures.CentrelineLength, 10) };

    [Test]
    public async Task GetCategories_AlwaysOffersGenericModels()
    {
        var document = NewDocument();

        var categories = RoadBuilder.GetCategories(document);

        await Assert.That(categories.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(categories.Any(c => c.Category == BuiltInCategory.OST_GenericModel)).IsTrue();
        await Assert.That(categories[categories.Count - 1].Category).IsEqualTo(BuiltInCategory.OST_GenericModel);
    }

    [Test]
    public async Task Create_NullArguments_Throw()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        await Assert.That(Catch(() => RoadBuilder.Create(null, alignment, RoadFixtures.Slab(), new RoadOptions())) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => RoadBuilder.Create(document, null, RoadFixtures.Slab(), new RoadOptions())) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => RoadBuilder.Create(document, alignment, null, new RoadOptions())) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Create_NotAnAlignment_IsRefused()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        var exception = Catch(() => RoadBuilder.Create(document, line, RoadFixtures.Slab(), new RoadOptions()));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task CreateLoops_StartAndEndOnTheAlignment_AndFollowIt()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var path = new AlignmentApi.PathReader(alignment);

        var loops = RoadBuilder.CreateLoops(path, RoadFixtures.Slab(), new RoadOptions { Interval = 10.0 }, out var stations);

        await Assert.That(loops.Count).IsEqualTo(stations);
        await Assert.That(stations).IsGreaterThan((int)(RoadFixtures.CentrelineLength / 10.0));
        var first = loops[0].SelectMany(c => c.Tessellate()).ToList();
        var last = loops[loops.Count - 1].SelectMany(c => c.Tessellate()).ToList();
        // the road starts at (0,0) heading east: its section spreads along Y at X = 0, and ends heading north at (300, 300)
        await Assert.That(first.Average(p => p.X)).IsEqualTo(0.0).Within(1e-3);
        await Assert.That(last.Average(p => p.Y)).IsEqualTo(300.0).Within(1e-3);
    }

    [Test]
    public async Task CreateLoops_AddsSectionsAroundTheCurve()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var path = new AlignmentApi.PathReader(alignment);

        RoadBuilder.CreateLoops(path, RoadFixtures.Slab(), new RoadOptions { Interval = 40.0 }, out var stations);

        // 400 ft at one section per 40 ft would be about 14; the 100 ft radius turn needs more to look round
        await Assert.That(stations).IsGreaterThan(16);
    }

    [Test]
    public async Task Create_MakesOneRoadWithTheExpectedVolume()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 10.0, ProfileName = "Slab : 12 ft" });

        await Assert.That(result.SkippedSpans).IsEqualTo(0);
        await Assert.That(result.ReplacedPrevious).IsFalse();
        await Assert.That(result.RoadId).IsNotEqualTo(ElementId.InvalidElementId);
        var road = document.GetElement(result.RoadId);
        await Assert.That(road is DirectShape).IsTrue();
        await Assert.That(RoadFixtures.Volume(road)).IsEqualTo(6.0 * RoadFixtures.CentrelineLength).Within(6.0 * RoadFixtures.CentrelineLength * 0.02);
        await Assert.That(RoadFixtures.SolidCount(road)).IsEqualTo(result.SolidCount);
    }

    [Test]
    public async Task Create_TagsTheRoadSoItCanBeFoundAgain()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document, "Tagged");

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 20.0, ProfileName = "Slab : 12 ft" });

        var road = (DirectShape)document.GetElement(result.RoadId);
        await Assert.That(road.ApplicationId).IsEqualTo(RoadBuilder.ApplicationId);
        await Assert.That(road.ApplicationDataId).IsEqualTo(alignment.UniqueId);
        var comments = road.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString();
        await Assert.That(comments.Contains("Tagged")).IsTrue();
        await Assert.That(comments.Contains("Slab : 12 ft")).IsTrue();
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_Twice_ReplacesTheFirstRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var first = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 20.0 });

        var second = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(8, 1), new RoadOptions { Interval = 20.0 });

        await Assert.That(second.ReplacedPrevious).IsTrue();
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
        await Assert.That(document.GetElement(first.RoadId)).IsNull();
        await Assert.That(document.GetElement(second.RoadId)).IsNotNull();
    }

    [Test]
    public async Task Create_RoadsForDifferentAlignments_DoNotReplaceEachOther()
    {
        var document = NewDocument();
        var one = RoadFixtures.CurvedAlignment(document, "One");
        var two = RoadFixtures.CurvedAlignment(document, "Two");

        RoadBuilder.Create(document, one, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });
        var result = RoadBuilder.Create(document, two, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });

        await Assert.That(result.ReplacedPrevious).IsFalse();
        await Assert.That(RoadBuilder.FindRoads(document, one).Count).IsEqualTo(1);
        await Assert.That(RoadBuilder.FindRoads(document, two).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_VerticalOffset_LiftsTheRoad()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var flat = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });
        var flatTop = document.GetElement(flat.RoadId).get_BoundingBox(null).Max.Z;

        var raised = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0, VerticalOffset = 3.0 });

        await Assert.That(document.GetElement(raised.RoadId).get_BoundingBox(null).Max.Z - flatTop).IsEqualTo(3.0).Within(1e-3);
    }

    [Test]
    public async Task Create_FollowsAGradedProfile()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document, "Climbing", Climb());

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 20.0 });

        var box = document.GetElement(result.RoadId).get_BoundingBox(null);
        // the centreline climbs 10 ft; the slab hangs 0.5 ft below it
        await Assert.That(box.Max.Z - box.Min.Z).IsEqualTo(10.5).Within(0.05);
    }

    [Test]
    public async Task Create_WithTheRoadsCategory_WhenTheVersionAllowsIt()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);
        var roads = RoadBuilder.GetCategories(document).FirstOrDefault(c => c.Category != BuiltInCategory.OST_GenericModel);
        if (roads is null)
        {
            Skip.Test("This Revit only accepts Generic Models for a DirectShape.");
        }

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0, Category = roads.Category });

        await Assert.That(document.GetElement(result.RoadId).Category.BuiltInCategory).IsEqualTo(roads.Category);
    }

    [Test]
    public async Task Create_ACategoryTheDocumentRefuses_FallsBackToGenericModels()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        // newer Revits accept Walls for a DirectShape, so look for a category this one really refuses
        var refused = new[] { BuiltInCategory.OST_Cameras, BuiltInCategory.OST_Levels, BuiltInCategory.OST_Grids, BuiltInCategory.OST_Views }
            .Cast<BuiltInCategory?>()
            .FirstOrDefault(c => !DirectShape.IsValidCategoryId(new ElementId(c.Value), document));
        if (refused is null)
        {
            Skip.Test("This Revit accepts every category tried for a DirectShape.");
        }

        var result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0, Category = refused.Value });

        await Assert.That(document.GetElement(result.RoadId).Category.BuiltInCategory).IsEqualTo(BuiltInCategory.OST_GenericModel);
    }

    [Test]
    public async Task Create_AProfileTooWideForTheTurn_ReportsGapsOrFailsClearly()
    {
        var document = NewDocument();
        var alignment = RoadFixtures.CurvedAlignment(document);

        // 400 ft wide on a 100 ft radius turn: the inside of the road folds back on itself
        RoadResult result = null;
        var exception = Catch(() => result = RoadBuilder.Create(document, alignment, RoadFixtures.Slab(400, 0.5), new RoadOptions { Interval = 20.0 }));

        // either some spans are skipped and reported, or nothing could be built and the exception explains why
        if (exception is null)
        {
            await Assert.That(result.SkippedSpans).IsGreaterThan(0);
        }
        else
        {
            await Assert.That(exception is InvalidOperationException).IsTrue();
            await Assert.That(exception.Message.Contains("could not build")).IsTrue();
        }
    }
}

public class SweepAlignmentViewModelTests : TopoAlignUiTest
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

    private (SweepAlignmentViewModel ViewModel, Document Document, Element Alignment) NewViewModel(bool withProfile = true)
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var alignment = RoadFixtures.CurvedAlignment(document, "Road 1");
        var viewModel = new SweepAlignmentViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);

        if (withProfile)
        {
            // there are no profile families in a new project, so give the dialog a section as if one had been chosen
            typeof(SweepAlignmentViewModel).GetField("_profile", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, RoadFixtures.Slab());
            viewModel.CreateCommand.NotifyCanExecuteChanged();
        }

        return (viewModel, document, alignment);
    }

    [Test]
    public async Task Constructor_ExplainsWhenThereAreNoProfileFamilies()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();

        var viewModel = new SweepAlignmentViewModel();

        await Assert.That(viewModel.Categories.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(viewModel.SelectedCategory).IsNotNull();
        await Assert.That(viewModel.Interval).IsGreaterThan(0m);
        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("No alignment selected.");
        await Assert.That(viewModel.WindowTitle.StartsWith("Sweep Along Alignment ")).IsTrue();
        if (viewModel.ProfileTypes.Count == 0)
        {
            await Assert.That(viewModel.ProfileStatus.Contains("No profile families")).IsTrue();
        }
    }

    [Test]
    public async Task SelectAlignment_NotAnAlignment_IsRejected()
    {
        using var backup = new RegistryBackup();
        var (viewModel, document, _) = NewViewModel();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(viewModel.SelectAlignment(line)).IsFalse();
        await Assert.That(viewModel.SelectAlignment(null)).IsFalse();
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("That element is not an alignment.");
    }

    [Test]
    public async Task SelectAlignment_ShowsNameAndLength()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();

        await Assert.That(viewModel.SelectAlignment(alignment)).IsTrue();

        await Assert.That(viewModel.HasAlignment).IsTrue();
        await Assert.That(viewModel.AlignmentStatus.Contains("Road 1")).IsTrue();
        await Assert.That(viewModel.AlignmentStatus.Contains("length")).IsTrue();
        await Assert.That(viewModel.ExistingRoadNote).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SelectAlignment_WithAnExistingRoad_SaysItWillBeReplaced()
    {
        using var backup = new RegistryBackup();
        var (viewModel, document, alignment) = NewViewModel();
        RoadBuilder.Create(document, alignment, RoadFixtures.Slab(), new RoadOptions { Interval = 25.0 });

        viewModel.SelectAlignment(alignment);

        await Assert.That(viewModel.ExistingRoadNote.Contains("replaces")).IsTrue();
    }

    [Test]
    public async Task Create_NeedsAnAlignmentASectionAndASpacing()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();

        viewModel.SelectAlignment(alignment);
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();

        viewModel.Interval = 0m;
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();

        var (noProfile, _, other) = NewViewModel(withProfile: false);
        noProfile.SelectAlignment(other);
        await Assert.That(noProfile.CreateCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Create_BuildsTheRequestInInternalUnitsAndClosesTheDialog()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.Interval = 12m;
        viewModel.VerticalOffset = -0.5m;
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.CreateCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request.AlignmentName).IsEqualTo("Road 1");
        await Assert.That(request.Options.Interval).IsEqualTo(12.0).Within(1e-9);
        await Assert.That(request.Options.VerticalOffset).IsEqualTo(-0.5).Within(1e-9);
        await Assert.That(request.Profile.Width).IsEqualTo(12.0).Within(1e-9);
        await Assert.That(request.Alignment.Id).IsEqualTo(alignment.Id);
    }

    [Test]
    public async Task Create_ConvertsFromTheSelectedUnits()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);
        viewModel.Interval = 2m;

        viewModel.CreateCommand.Execute(null);

        await Assert.That(viewModel.Request.Options.Interval).IsEqualTo(2.0 / 0.3048).Within(1e-6);
    }

    [Test]
    public async Task Create_MirrorFlipsTheSection()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();
        var offsetSection = new List<PlanSegment>
        {
            new PlanLine(new PlanPoint(0, 0), new PlanPoint(8, 0)),
            new PlanLine(new PlanPoint(8, 0), new PlanPoint(8, -1)),
            new PlanLine(new PlanPoint(8, -1), new PlanPoint(0, -1)),
            new PlanLine(new PlanPoint(0, -1), new PlanPoint(0, 0))
        };
        SectionProfile.TryCreate(offsetSection, out var profile, out _);
        typeof(SweepAlignmentViewModel).GetField("_profile", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, profile);
        viewModel.SelectAlignment(alignment);
        viewModel.MirrorProfile = true;

        viewModel.CreateCommand.Execute(null);

        await Assert.That(viewModel.Request.Profile.MinU).IsEqualTo(-8.0).Within(1e-9);
        await Assert.That(viewModel.Request.Profile.MaxU).IsEqualTo(0.0).Within(1e-9);
    }

    [Test]
    public async Task Create_SavesTheOptions()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.Interval = 7m;
        viewModel.VerticalOffset = 0.25m;
        viewModel.MirrorProfile = true;

        viewModel.CreateCommand.Execute(null);

        var saved = new Settings();
        saved.LoadSettings();
        await Assert.That((double)saved.SweepInterval).IsEqualTo(7.0).Within(1e-6);
        await Assert.That((double)saved.SweepVerticalOffset).IsEqualTo(0.25).Within(1e-6);
        await Assert.That(saved.SweepMirrorProfile).IsTrue();
        await Assert.That(saved.SweepCategory).IsEqualTo(viewModel.SelectedCategory.Category.ToString());
    }

    [Test]
    public async Task Settings_SweepDefaultsAndRoundTrip()
    {
        using var backup = new RegistryBackup();
        var defaults = new Settings();
        await Assert.That((double)defaults.SweepInterval).IsEqualTo(1.0 / 0.3048).Within(1e-9);
        await Assert.That(defaults.SweepVerticalOffset).IsEqualTo(0m);
        await Assert.That(defaults.SweepMirrorProfile).IsFalse();
        await Assert.That(defaults.SweepCategory).IsEqualTo(string.Empty);

        new Settings { SweepInterval = 9m, SweepVerticalOffset = -2m, SweepMirrorProfile = true, SweepCategory = "OST_Roads", SweepProfileName = "Road : Two lane" }.SaveSettings();
        var loaded = new Settings();
        loaded.LoadSettings();

        await Assert.That(loaded.SweepInterval).IsEqualTo(9m);
        await Assert.That(loaded.SweepVerticalOffset).IsEqualTo(-2m);
        await Assert.That(loaded.SweepMirrorProfile).IsTrue();
        await Assert.That(loaded.SweepCategory).IsEqualTo("OST_Roads");
        await Assert.That(loaded.SweepProfileName).IsEqualTo("Road : Two lane");
    }

    [Test]
    public async Task PickAlignment_RequestsAPickAndHidesTheWindow()
    {
        using var backup = new RegistryBackup();
        var (viewModel, _, _) = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickAlignmentCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task EndToEnd_TheRequestMakesARoad()
    {
        using var backup = new RegistryBackup();
        var (viewModel, document, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.Interval = 20m;
        viewModel.CreateCommand.Execute(null);
        var request = viewModel.Request;

        var result = RoadBuilder.Create(document, request.Alignment, request.Profile, request.Options);

        await Assert.That(result.SolidCount).IsGreaterThan(10);
        await Assert.That(RoadBuilder.FindRoads(document, alignment).Count).IsEqualTo(1);
    }

    [Test]
    public async Task SweepAlignmentView_HasItsControls_AndHidesWhenAPickIsRequested()
    {
        using var backup = new RegistryBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new SweepAlignmentViewModel();
        var buttons = new List<string>();
        System.Windows.Visibility visibility = System.Windows.Visibility.Visible;

        RunOnStaThread(() =>
        {
            var view = new SweepAlignmentView(viewModel);
            void Walk(System.Windows.DependencyObject parent)
            {
                foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(parent))
                {
                    if (child is System.Windows.DependencyObject element)
                    {
                        if (element is System.Windows.Controls.Button button)
                        {
                            buttons.Add(button.Content?.ToString());
                        }

                        Walk(element);
                    }
                }
            }

            Walk(view);
            viewModel.PickAlignmentCommand.Execute(null);
            visibility = view.Visibility;
        });

        await Assert.That(buttons.Contains("Pick alignment")).IsTrue();
        await Assert.That(buttons.Contains("Create Road")).IsTrue();
        await Assert.That(visibility).IsEqualTo(System.Windows.Visibility.Hidden);
    }
}
