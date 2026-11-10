using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Text.Json.Nodes;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Profile;
using TopoAlign.Geometry.Topography;

namespace TopoAlign.Tests;

public class ProfileSamplerTests : TopoAlignTest
{
    /// <summary>A flat surface at the given elevation covering (-1000,-1000)-(1000,1000).</summary>
    private static TriangleSurface FlatSurface(double elevation)
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(-1000, -1000, elevation, 1000, -1000, elevation, 1000, 1000, elevation);
        surface.AddTriangle(-1000, -1000, elevation, 1000, 1000, elevation, -1000, 1000, elevation);
        return surface;
    }

    [Test]
    public async Task Sample_NoCurves_Throws()
    {
        var exception = Catch(() => ProfileSampler.Sample(new List<Curve>(), FlatSurface(5), 10, 0));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Sample_Line_SamplesEveryIntervalAndTheEnd()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 100, 0) }, FlatSurface(12), 25, 0);

        await Assert.That(result.Samples.Count).IsEqualTo(5);
        var stations = result.Samples.Select(s => s.Station).ToArray();
        await Assert.That(stations[0]).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(stations[1]).IsEqualTo(25.0).Within(1e-9);
        await Assert.That(stations[4]).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(result.Samples.All(s => Math.Abs(s.Elevation - 12) < 1e-9)).IsTrue();
        await Assert.That(result.MissingCount).IsEqualTo(0);
    }

    [Test]
    public async Task Sample_IntervalThatDoesNotDivideTheLength_StillSamplesTheEnd()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 100, 0) }, FlatSurface(0), 30, 0);

        var stations = result.Samples.Select(s => s.Station).ToArray();
        await Assert.That(stations.Length).IsEqualTo(5);
        await Assert.That(stations[3]).IsEqualTo(90.0).Within(1e-9);
        await Assert.That(stations[4]).IsEqualTo(100.0).Within(1e-9);
    }

    [Test]
    public async Task Sample_AddsTheElevationOffset()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 50, 0) }, FlatSurface(20), 10, 1.5);

        await Assert.That(result.Samples.All(s => Math.Abs(s.Elevation - 21.5) < 1e-9)).IsTrue();
    }

    [Test]
    public async Task Sample_NegativeOffsetLowersTheProfile()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 50, 0) }, FlatSurface(20), 10, -4);

        await Assert.That(result.Samples[0].Elevation).IsEqualTo(16.0).Within(1e-9);
    }

    [Test]
    public async Task Sample_Chain_StationsContinueAcrossCurves()
    {
        var curves = new Curve[] { Ln(0, 0, 100, 0), Ln(100, 0, 100, 50) };

        var result = ProfileSampler.Sample(curves, FlatSurface(3), 40, 0);

        var stations = result.Samples.Select(s => s.Station).ToList();
        await Assert.That(stations[0]).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(stations[stations.Count - 1]).IsEqualTo(150.0).Within(1e-9);
        // the join at 100 is sampled once, not twice
        await Assert.That(stations.Count(s => Math.Abs(s - 100.0) < 1e-9)).IsEqualTo(1);
        for (var i = 1; i < stations.Count; i++)
        {
            await Assert.That(stations[i]).IsGreaterThan(stations[i - 1]);
        }
    }

    [Test]
    public async Task Sample_Arc_StationsFollowTheArcLength()
    {
        var curves = new Curve[] { Ln(30, 0, 10, 0), QuarterArcCcw(10) };

        var result = ProfileSampler.Sample(curves, FlatSurface(0), 5, 0);

        var expected = 20.0 + Math.PI * 10.0 / 2.0;
        await Assert.That(result.Samples[result.Samples.Count - 1].Station).IsEqualTo(expected).Within(1e-6);
    }

    [Test]
    public async Task Sample_SlopedSurface_ReadsTheGroundAtEachStation()
    {
        // z = 0.1 * x on a 2000 x 2000 area
        var surface = new TriangleSurface();
        surface.AddTriangle(-1000, -1000, -100, 1000, -1000, 100, 1000, 1000, 100);
        surface.AddTriangle(-1000, -1000, -100, 1000, 1000, 100, -1000, 1000, -100);

        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 200, 0) }, surface, 50, 0);

        foreach (var sample in result.Samples)
        {
            await Assert.That(sample.Elevation).IsEqualTo(0.1 * sample.Station).Within(1e-9);
        }
    }

    [Test]
    public async Task Sample_LineLeavingTheSurface_ThrowsBecauseTheEndIsOff()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(-10, -10, 0, 60, -10, 0, 60, 10, 0);
        surface.AddTriangle(-10, -10, 0, 60, 10, 0, -10, 10, 0);

        var exception = Catch(() => ProfileSampler.Sample(new Curve[] { Ln(0, 0, 100, 0) }, surface, 10, 0));

        await Assert.That(exception is InvalidOperationException).IsTrue();
        await Assert.That(exception.Message.Contains("outside")).IsTrue();
    }

    [Test]
    public async Task Sample_StartOffTheSurface_Throws()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(40, -10, 0, 200, -10, 0, 200, 10, 0);
        surface.AddTriangle(40, -10, 0, 200, 10, 0, 40, 10, 0);

        var exception = Catch(() => ProfileSampler.Sample(new Curve[] { Ln(0, 0, 100, 0) }, surface, 10, 0));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task Sample_GapInTheMiddle_IsBridgedAndCounted()
    {
        // two patches with a hole between x = 40 and x = 60
        var surface = new TriangleSurface();
        surface.AddTriangle(-10, -10, 5, 40, -10, 5, 40, 10, 5);
        surface.AddTriangle(-10, -10, 5, 40, 10, 5, -10, 10, 5);
        surface.AddTriangle(60, -10, 5, 110, -10, 5, 110, 10, 5);
        surface.AddTriangle(60, -10, 5, 110, 10, 5, 60, 10, 5);

        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 100, 0) }, surface, 10, 0);

        await Assert.That(result.MissingCount).IsEqualTo(1); // only 50 is inside the hole; 40 and 60 sit on its edges
        await Assert.That(result.Samples.Any(s => Math.Abs(s.Station - 50) < 1e-9)).IsFalse();
        await Assert.That(result.Samples[0].Station).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(result.Samples[result.Samples.Count - 1].Station).IsEqualTo(100.0).Within(1e-9);
    }

    [Test]
    public async Task Sample_TinyInterval_DoesNotLoopForever()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 10, 0) }, FlatSurface(0), 0, 0);

        await Assert.That(result.Samples.Count).IsGreaterThan(2);
        await Assert.That(result.Samples.Count).IsLessThan(2000);
    }

    [Test]
    public async Task Sample_IgnoresTheCurvesOwnElevation()
    {
        var result = ProfileSampler.Sample(new Curve[] { Ln(0, 0, 50, 0, 99) }, FlatSurface(4), 25, 0);

        await Assert.That(result.Samples[0].Elevation).IsEqualTo(4.0).Within(1e-9);
    }
}

public class TopographyReaderTests : TopoAlignTest
{
    private static DirectShape ShapeFromSolid(Document document, Solid solid)
    {
        using var transaction = new Transaction(document, "Create shape");
        transaction.Start();
        var shape = DirectShape.CreateElement(document, new ElementId(BuiltInCategory.OST_GenericModel));
        shape.SetShape(new List<GeometryObject> { solid });
        transaction.Commit();
        return shape;
    }

    [Test]
    public async Task Read_Null_Throws()
    {
        await Assert.That(Catch(() => TopographyReader.Read(null)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Read_ElementWithoutSurfaceGeometry_Throws()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        var exception = Catch(() => TopographyReader.Read(line));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task Read_Box_GivesTheTopFaceElevation()
    {
        var document = NewDocument();
        var shape = ShapeFromSolid(document, Box(100, 100, 10));

        var surface = TopographyReader.Read(shape);

        await Assert.That(surface.TryGetElevation(50, 50, out var z)).IsTrue();
        await Assert.That(z).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(surface.TryGetElevation(150, 50, out _)).IsFalse();
    }

    [Test]
    public async Task Read_Box_IgnoresBottomAndSideFaces()
    {
        var document = NewDocument();
        var shape = ShapeFromSolid(document, Box(100, 100, 10));

        var surface = TopographyReader.Read(shape);

        // a downward or vertical face would add triangles at z = 0 or zero-area walls; only the top (two triangles) counts
        await Assert.That(surface.TriangleCount).IsGreaterThan(0);
        await Assert.That(surface.TryGetElevation(20, 80, out var z)).IsTrue();
        await Assert.That(z).IsEqualTo(10.0).Within(1e-6);
    }

    [Test]
    public async Task Read_Cylinder_CoversTheCurvedTop()
    {
        var document = NewDocument();
        var shape = ShapeFromSolid(document, Cylinder(20, 6));

        var surface = TopographyReader.Read(shape);

        await Assert.That(surface.TryGetElevation(0, 0, out var centre)).IsTrue();
        await Assert.That(centre).IsEqualTo(6.0).Within(1e-6);
        await Assert.That(surface.TryGetElevation(15, 5, out var inside)).IsTrue();
        await Assert.That(inside).IsEqualTo(6.0).Within(1e-6);
        await Assert.That(surface.TryGetElevation(25, 0, out _)).IsFalse();
    }
}

public class AlignmentJsonBuilderProfileTests : TopoAlignTest
{
    private static List<ProfilePoint> Pvis(params (double Station, double Elevation)[] points)
    {
        return points.Select(p => new ProfilePoint(p.Station, p.Elevation)).ToList();
    }

    private static JsonArray ProfileCurves(string json) => Curves(ProfileGeometry(Parse(json)));

    private static readonly Curve[] Straight = { Ln(0, 0, 100, 0) };

    [Test]
    public async Task Build_PolylineProfile_WritesALinePerSegment()
    {
        var json = AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0, 10), (40, 14), (100, 8)), 0.0, out var length);

        var profile = ProfileCurves(json);

        await Assert.That(length).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(profile.Count).IsEqualTo(2);
        await Assert.That(TypeId(profile[0]!)).IsEqualTo(LineTypeId);
        var first = PointOf(profile[0]!, Point2DKey, "position");
        var firstDirection = PointOf(profile[0]!, Vector2DKey, "direction");
        await Assert.That(first.X).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(first.Y).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(firstDirection.X).IsEqualTo(40.0).Within(1e-9);
        await Assert.That(firstDirection.Y).IsEqualTo(4.0).Within(1e-9);
        var second = PointOf(profile[1]!, Point2DKey, "position");
        await Assert.That(second.X).IsEqualTo(40.0).Within(1e-9);
        await Assert.That(second.Y).IsEqualTo(14.0).Within(1e-9);
    }

    [Test]
    public async Task Build_WithVerticalCurveLength_WritesAParabolicCurveAtTheBend()
    {
        var json = AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0, 0), (50, 5), (100, 0)), 20.0, out _);

        var profile = ProfileCurves(json);

        await Assert.That(profile.Count).IsEqualTo(3);
        var curve = profile[1]!;
        await Assert.That(TypeId(curve)).IsEqualTo("autodesk.infra.geometry:verticalCurve-1.0.0");
        var start = PointOf(curve, Point2DKey, "startPoint");
        var pvi = PointOf(curve, Point2DKey, "pvi");
        var end = PointOf(curve, Point2DKey, "endPoint");
        await Assert.That(start.X).IsEqualTo(40.0).Within(1e-9);
        await Assert.That(start.Y).IsEqualTo(4.0).Within(1e-9);
        await Assert.That(pvi.X).IsEqualTo(50.0).Within(1e-9);
        await Assert.That(pvi.Y).IsEqualTo(5.0).Within(1e-9);
        await Assert.That(end.X).IsEqualTo(60.0).Within(1e-9);
        await Assert.That(end.Y).IsEqualTo(4.0).Within(1e-9);
        await Assert.That(RangeOf(curve).Type).IsEqualTo(0);
    }

    [Test]
    public async Task Build_ProfileElements_AreEndToEnd()
    {
        var json = AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0, 0), (30, 6), (70, -2), (100, 4)), 16.0, out _);

        var profile = ProfileCurves(json);

        var cursorStation = 0.0;
        var cursorElevation = 0.0;
        foreach (var element in profile)
        {
            double startStation, startElevation, endStation, endElevation;
            if (TypeId(element!) == LineTypeId)
            {
                var position = PointOf(element!, Point2DKey, "position");
                var direction = PointOf(element!, Vector2DKey, "direction");
                (startStation, startElevation) = position;
                (endStation, endElevation) = (position.X + direction.X, position.Y + direction.Y);
            }
            else
            {
                (startStation, startElevation) = PointOf(element!, Point2DKey, "startPoint");
                (endStation, endElevation) = PointOf(element!, Point2DKey, "endPoint");
            }

            await Assert.That(startStation).IsEqualTo(cursorStation).Within(1e-9);
            await Assert.That(startElevation).IsEqualTo(cursorElevation).Within(1e-9);
            cursorStation = endStation;
            cursorElevation = endElevation;
        }

        await Assert.That(cursorStation).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(cursorElevation).IsEqualTo(4.0).Within(1e-9);
    }

    [Test]
    public async Task Build_Profile_UsesTheProjectLocationsVerticalOffset()
    {
        // internal elevation 10, shared offset +100 (internal = shared - 100)
        var internalToShared = Transform.CreateTranslation(new XYZ(0, 0, 100));

        var json = AlignmentJsonBuilder.Build("T", Straight, internalToShared, 0.0, Pvis((0, 10), (100, 20)), 0.0, out _);

        var line = ProfileCurves(json)[0]!;
        await Assert.That(PointOf(line, Point2DKey, "position").Y).IsEqualTo(110.0).Within(1e-9);
        await Assert.That(PointOf(line, Vector2DKey, "direction").Y).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task Build_Profile_LocksItsEndsToTheWrittenPlanLength()
    {
        var json = AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0.0004, 5), (99.9996, 5)), 0.0, out var length);

        var line = ProfileCurves(json)[0]!;

        await Assert.That(PointOf(line, Point2DKey, "position").X).IsEqualTo(0.0);
        await Assert.That(PointOf(line, Vector2DKey, "direction").X).IsEqualTo(length).Within(1e-12);
    }

    [Test]
    public async Task Build_ProfileNotReachingTheEnd_Throws()
    {
        var exception = Catch(() => AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0, 5), (60, 5)), 0.0, out _));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Build_ProfileNotStartingAtZero_Throws()
    {
        var exception = Catch(() => AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((10, 5), (100, 5)), 0.0, out _));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Build_ProfileWithASinglePoint_Throws()
    {
        var exception = Catch(() => AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0,
            Pvis((0, 5)), 0.0, out _));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Build_NullProfile_StaysFlat()
    {
        var json = AlignmentJsonBuilder.Build("T", Straight, Transform.Identity, 0.0, null, 0.0, out _);

        var profile = ProfileCurves(json);

        await Assert.That(profile.Count).IsEqualTo(1);
        await Assert.That(PointOf(profile[0]!, Vector2DKey, "direction").Y).IsEqualTo(0.0);
    }

    [Test]
    public async Task Build_Profile_WritesEveryFloatWithADecimalPoint()
    {
        var json = AlignmentJsonBuilder.Build("T", new Curve[] { Ln(0, 0, 100, 0) }, Transform.Identity, 1000.0,
            Pvis((0, 10), (50, 20), (100, 10)), 20.0, out _);

        var reader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        var property = string.Empty;
        var offenders = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                property = reader.GetString();
            }
            else if (reader.TokenType == System.Text.Json.JsonTokenType.Number && property != "type")
            {
                var text = System.Text.Encoding.UTF8.GetString(reader.ValueSpan.ToArray());
                if (!text.Contains('.') && !text.Contains('E') && !text.Contains('e'))
                {
                    offenders.Add($"{property}={text}");
                }
            }
        }

        await Assert.That(offenders.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_ProfileCurves_CreateAValidNativeAlignment()
    {
        var document = NewDocument();
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var curves = new Curve[] { Ln(0, 0, 200, 0), Ln(200, 0, 200, 100) };

        var flatJson = AlignmentJsonBuilder.Build("Flat", curves, internalToShared, 500.0, out var flatLength);
        var flatId = AlignmentApi.Create(document, "Flat", flatJson, flatLength);

        var drapedJson = AlignmentJsonBuilder.Build("Draped", curves, internalToShared, 500.0,
            Pvis((0, 10), (80, 18), (190, 12), (300, 20)), 30.0, out var length);
        var drapedId = AlignmentApi.Create(document, "Draped", drapedJson, length);

        await Assert.That(drapedId).IsNotEqualTo(ElementId.InvalidElementId);
        var flat = AlignmentApi.GetInfo(document.GetElement(flatId));
        var draped = AlignmentApi.GetInfo(document.GetElement(drapedId));
        await Assert.That(draped).IsNotNull();
        await Assert.That(draped.Name).IsEqualTo("Draped");
        // the profile changes elevations only: stationing matches the flat alignment on the same lines
        await Assert.That(draped.StartStation).IsEqualTo(flat.StartStation).Within(1e-6);
        await Assert.That(draped.EndStation).IsEqualTo(flat.EndStation).Within(1e-6);
    }

    [Test]
    public async Task Build_ProfileCurves_KeepThePlanGeometry()
    {
        var document = NewDocument();
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var curves = new Curve[] { Ln(0, 0, 100, 0) };
        var json = AlignmentJsonBuilder.Build("Plan", curves, internalToShared, 0.0,
            Pvis((0, 10), (100, 20)), 0.0, out var length);

        var element = document.GetElement(AlignmentApi.Create(document, "Plan", json, length));

        var info = AlignmentApi.GetInfo(element);
        var middle = AlignmentApi.GetPointAtStation(element, info.StartStation + (info.EndStation - info.StartStation) / 2.0);
        await Assert.That(middle).IsNotNull();
        await Assert.That(middle.X).IsEqualTo(50.0).Within(1e-3);
        await Assert.That(middle.Y).IsEqualTo(0.0).Within(1e-3);
    }
}
