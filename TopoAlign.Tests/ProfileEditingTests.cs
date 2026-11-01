using static TopoAlign.Tests.TestKit;
using System.Text.Json.Nodes;
using TopoAlign.Alignments;

namespace TopoAlign.Tests;

/// <summary>Editing the vertical profile of an existing alignment's JSON: reading PVIs, adding stations, rewriting. Needs no Revit model.</summary>
public class ProfileEditingTests : TopoAlignTest
{
    private const double Length = 1000.0;

    private static ProfilePoint P(double station, double elevation) => new ProfilePoint(station, elevation);

    private static ProfileVertex V(double distance, double elevation, double curveLength = 0.0) => new ProfileVertex(distance, elevation, curveLength);

    private static JsonObject Geometry(JsonArray curves) => new JsonObject
    {
        [InfraJson.CurveArrayKey] = new JsonObject { ["curves"] = curves }
    };

    /// <summary>A straight alignment 1000 ft long, with the given profile curves (default: a flat profile at 100 ft).</summary>
    private static string Document(JsonArray profile = null, double startStation = 0.0)
    {
        var horizontal = new JsonArray { ProfileJson.CreateLineNode(0.0, 0.0, Length, 0.0) };
        profile ??= new JsonArray { ProfileJson.CreateLineNode(0.0, 100.0, Length, 0.0) };

        var root = new JsonObject
        {
            [InfraJson.AlignmentKey] = new JsonObject
            {
                ["CoreComponent"] = new JsonObject
                {
                    ["Float64"] = new JsonObject { ["startstation"] = InfraJson.Number(startStation) },
                    ["String"] = new JsonObject { ["name"] = "Road", ["guid"] = "core-guid" },
                    [InfraJson.CurveGeometryKey] = new JsonObject
                    {
                        ["curveGeometry"] = new JsonObject
                        {
                            [InfraJson.HorizontalGeometryKey] = new JsonObject { ["horizontalGeometry"] = Geometry(horizontal) },
                            [InfraJson.ProfileGeometryKey] = new JsonObject { ["profileGeometry"] = Geometry(profile) }
                        }
                    }
                }
            }
        };

        return root.ToJsonString();
    }

    private static string Curved()
    {
        var pvis = new[] { P(0, 100), P(400, 110), P(700, 104), P(1000, 120) };
        return Document(ProfileJson.CreateCurves(pvis, new[] { 0.0, 80.0, 60.0, 0.0 }));
    }

    // ---- ProfileLayout with a curve length per PVI ---------------------------------------------------------------

    [Test]
    public async Task Layout_PerPviLengths_GiveEachCurveItsOwnLength()
    {
        var elements = ProfileLayout.Create(new[] { P(0, 100), P(400, 110), P(700, 104), P(1000, 120) }, new[] { 0.0, 80.0, 60.0, 0.0 });

        var curves = elements.OfType<ProfileParabola>().ToList();
        await Assert.That(curves.Count).IsEqualTo(2);
        await Assert.That(curves[0].End.Station - curves[0].Start.Station).IsEqualTo(80.0).Within(1e-9);
        await Assert.That(curves[1].End.Station - curves[1].Start.Station).IsEqualTo(60.0).Within(1e-9);
    }

    [Test]
    public async Task Layout_PerPviLengths_MatchTheSingleLengthOverload()
    {
        var pvis = new[] { P(0, 100), P(400, 110), P(700, 104), P(1000, 120) };

        var single = ProfileLayout.Create(pvis, 50.0);
        var each = ProfileLayout.Create(pvis, new[] { 0.0, 50.0, 50.0, 0.0 });

        await Assert.That(each.Count).IsEqualTo(single.Count);
        await Assert.That(each.OfType<ProfileParabola>().Count()).IsEqualTo(single.OfType<ProfileParabola>().Count());
    }

    [Test]
    public async Task Layout_PerPviLengths_WrongCountThrows()
    {
        var thrown = Catch(() => ProfileLayout.Create(new[] { P(0, 100), P(500, 110), P(1000, 120) }, new[] { 0.0, 10.0 }));

        await Assert.That(thrown is ArgumentException).IsTrue();
    }

    // ---- reading the profile -------------------------------------------------------------------------------------

    [Test]
    public async Task ReadProfile_FlatProfile_IsTwoPoints()
    {
        var vertices = AlignmentJsonDocument.Parse(Document()).ReadProfile();

        await Assert.That(vertices.Count).IsEqualTo(2);
        await Assert.That(vertices[0].Distance).IsEqualTo(0.0);
        await Assert.That(vertices[0].Elevation).IsEqualTo(100.0);
        await Assert.That(vertices[1].Distance).IsEqualTo(Length).Within(1e-9);
        await Assert.That(vertices[1].Elevation).IsEqualTo(100.0);
    }

    [Test]
    public async Task ReadProfile_CurvedProfile_RecoversPvisAndCurveLengths()
    {
        var vertices = AlignmentJsonDocument.Parse(Curved()).ReadProfile();

        await Assert.That(vertices.Count).IsEqualTo(4);
        await Assert.That(vertices[1].Distance).IsEqualTo(400.0).Within(1e-9);
        await Assert.That(vertices[1].Elevation).IsEqualTo(110.0).Within(1e-9);
        await Assert.That(vertices[1].CurveLength).IsEqualTo(80.0).Within(1e-9);
        await Assert.That(vertices[2].Distance).IsEqualTo(700.0).Within(1e-9);
        await Assert.That(vertices[2].CurveLength).IsEqualTo(60.0).Within(1e-9);
        await Assert.That(vertices[3].Distance).IsEqualTo(Length).Within(1e-9);
        await Assert.That(vertices[3].CurveLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task ReadProfile_SharpChangeOfGrade_IsAPviWithNoCurve()
    {
        var json = Document(ProfileJson.CreateCurves(new[] { P(0, 100), P(500, 120), P(1000, 100) }, 0.0));

        var vertices = AlignmentJsonDocument.Parse(json).ReadProfile();

        await Assert.That(vertices.Count).IsEqualTo(3);
        await Assert.That(vertices[1].Distance).IsEqualTo(500.0).Within(1e-9);
        await Assert.That(vertices[1].CurveLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task ReadProfile_ProfileLength_IsTheLastDistance()
    {
        await Assert.That(AlignmentJsonDocument.Parse(Curved()).ProfileLength).IsEqualTo(Length).Within(1e-9);
    }

    [Test]
    public async Task ReadProfile_ProfileThatStartsPartWayAlong_IsMeasuredFromItsStart()
    {
        var json = Document(new JsonArray { ProfileJson.CreateLineNode(500.0, 100.0, Length, 20.0) });

        var vertices = AlignmentJsonDocument.Parse(json).ReadProfile();

        await Assert.That(vertices[0].Distance).IsEqualTo(0.0);
        await Assert.That(vertices[1].Distance).IsEqualTo(Length).Within(1e-9);
        await Assert.That(vertices[1].Elevation).IsEqualTo(120.0).Within(1e-9);
    }

    [Test]
    public async Task CanEditProfile_ArcInTheProfile_IsRefusedWithAReason()
    {
        var arc = new JsonObject { ["typeid"] = InfraJson.CircleTypeId };
        var json = Document(new JsonArray { ProfileJson.CreateLineNode(0.0, 100.0, 500.0, 0.0), arc });

        var document = AlignmentJsonDocument.Parse(json);

        await Assert.That(document.CanEditProfile(out var reason)).IsFalse();
        await Assert.That(reason.Contains("circle2d")).IsTrue();
        await Assert.That(Catch(() => document.ReadProfile()) is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task CanEditProfile_NoProfile_IsRefused()
    {
        var document = AlignmentJsonDocument.Parse(Document(new JsonArray()));

        await Assert.That(document.CanEditProfile(out var reason)).IsFalse();
        await Assert.That(reason.Contains("no vertical profile")).IsTrue();
    }

    // ---- writing the profile -------------------------------------------------------------------------------------

    [Test]
    public async Task SetProfile_RoundTripWithoutChanges_KeepsThePvis()
    {
        var document = AlignmentJsonDocument.Parse(Curved());
        var before = document.ReadProfile();

        document.SetProfile(before);
        var after = AlignmentJsonDocument.Parse(document.ToJson()).ReadProfile();

        await Assert.That(after.Count).IsEqualTo(before.Count);
        for (var i = 0; i < before.Count; i++)
        {
            await Assert.That(after[i].Distance).IsEqualTo(before[i].Distance).Within(1e-9);
            await Assert.That(after[i].Elevation).IsEqualTo(before[i].Elevation).Within(1e-9);
            await Assert.That(after[i].CurveLength).IsEqualTo(before[i].CurveLength).Within(1e-9);
        }
    }

    [Test]
    public async Task SetProfile_AddedStation_AppearsInTheReadBackProfile()
    {
        var document = AlignmentJsonDocument.Parse(Document());
        var edited = ProfileEditing.Insert(document.ReadProfile(), 250.0, 40.0);
        edited[1] = edited[1].WithElevation(112.0);

        document.SetProfile(edited);
        var after = AlignmentJsonDocument.Parse(document.ToJson()).ReadProfile();

        await Assert.That(after.Count).IsEqualTo(3);
        await Assert.That(after[1].Distance).IsEqualTo(250.0).Within(1e-9);
        await Assert.That(after[1].Elevation).IsEqualTo(112.0).Within(1e-9);
        await Assert.That(after[1].CurveLength).IsEqualTo(40.0).Within(1e-9);
    }

    [Test]
    public async Task SetProfile_ChangedEndElevation_IsKeptAndTheEndsStayPut()
    {
        var document = AlignmentJsonDocument.Parse(Document());
        var edited = document.ReadProfile();
        edited[1] = edited[1].WithElevation(130.0);

        document.SetProfile(edited);
        var after = AlignmentJsonDocument.Parse(document.ToJson()).ReadProfile();

        await Assert.That(after[0].Distance).IsEqualTo(0.0);
        await Assert.That(after[1].Distance).IsEqualTo(Length).Within(1e-9);
        await Assert.That(after[1].Elevation).IsEqualTo(130.0).Within(1e-9);
    }

    [Test]
    public async Task SetProfile_KeepsThePlanNameStartStationAndGuid()
    {
        var document = AlignmentJsonDocument.Parse(Document(startStation: 250.0));
        var edited = ProfileEditing.Insert(document.ReadProfile(), 500.0, 0.0);

        document.SetProfile(edited);
        var after = AlignmentJsonDocument.Parse(document.ToJson());

        await Assert.That(after.Name).IsEqualTo("Road");
        await Assert.That(after.StartStation).IsEqualTo(250.0);
        await Assert.That(after.PlanLength).IsEqualTo(Length).Within(1e-9);
        await Assert.That(document.ToJson().Contains("core-guid")).IsTrue();
    }

    [Test]
    public async Task SetProfile_ProfileThatStartedPartWayAlong_KeepsItsOrigin()
    {
        var document = AlignmentJsonDocument.Parse(Document(new JsonArray { ProfileJson.CreateLineNode(500.0, 100.0, Length, 0.0) }));
        var edited = ProfileEditing.Insert(document.ReadProfile(), 300.0, 0.0);
        edited[1] = edited[1].WithElevation(105.0);

        document.SetProfile(edited);
        var json = document.ToJson();
        var after = AlignmentJsonDocument.Parse(json).ReadProfile();

        await Assert.That(after[0].Distance).IsEqualTo(0.0);
        await Assert.That(after[1].Distance).IsEqualTo(300.0).Within(1e-9);
        await Assert.That(after[1].Elevation).IsEqualTo(105.0).Within(1e-9);
        await Assert.That(json.Contains("\"x\":500.0")).IsTrue();
    }

    [Test]
    public async Task SetProfile_WrittenNumbersAllHaveADecimalPoint()
    {
        var document = AlignmentJsonDocument.Parse(Document());
        document.SetProfile(ProfileEditing.Insert(document.ReadProfile(), 400.0, 20.0));

        var json = document.ToJson();

        // a whole-number Float64 written as 400 rather than 400.0 would make Autodesk's parser reject the document
        await Assert.That(json.Contains("\"x\":400,")).IsFalse();
        await Assert.That(json.Contains("\"y\":100,")).IsFalse();
        await Assert.That(json.Contains("\"x\":400.0")).IsTrue();
    }

    [Test]
    public async Task SetProfile_InvalidPoints_Throw()
    {
        var document = AlignmentJsonDocument.Parse(Document());

        var movedEnd = new[] { V(0, 100), V(900, 100) };
        var outOfOrder = new[] { V(0, 100), V(600, 100), V(300, 100), V(Length, 100) };

        await Assert.That(Catch(() => document.SetProfile(movedEnd)) is InvalidOperationException).IsTrue();
        await Assert.That(Catch(() => document.SetProfile(outOfOrder)) is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task SetProfile_ThenReverse_StillMirrorsTheProfile()
    {
        var document = AlignmentJsonDocument.Parse(Document());
        var edited = ProfileEditing.Insert(document.ReadProfile(), 250.0, 0.0);
        edited[1] = edited[1].WithElevation(110.0);
        document.SetProfile(edited);

        document.Reverse();
        var after = document.ReadProfile();

        await Assert.That(after.Count).IsEqualTo(3);
        await Assert.That(after[1].Distance).IsEqualTo(750.0).Within(1e-9);
        await Assert.That(after[1].Elevation).IsEqualTo(110.0).Within(1e-9);
    }

    // ---- ProfileEditing helpers ----------------------------------------------------------------------------------

    [Test]
    public async Task Insert_PutsTheStationOnTheGradeLine()
    {
        var result = ProfileEditing.Insert(new[] { V(0, 100), V(1000, 120) }, 250.0, 0.0);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[1].Distance).IsEqualTo(250.0);
        await Assert.That(result[1].Elevation).IsEqualTo(105.0).Within(1e-9);
    }

    [Test]
    public async Task Insert_KeepsStationsInOrder()
    {
        var result = ProfileEditing.Insert(new[] { V(0, 100), V(300, 110), V(1000, 120) }, 600.0, 0.0);

        await Assert.That(result.Select(v => v.Distance).SequenceEqual(new[] { 0.0, 300.0, 600.0, 1000.0 })).IsTrue();
        await Assert.That(result[2].Elevation).IsEqualTo(110.0 + (600.0 - 300.0) / 700.0 * 10.0).Within(1e-9);
    }

    [Test]
    public async Task Insert_DoesNotChangeTheInputList()
    {
        var input = new[] { V(0, 100), V(1000, 120) };

        ProfileEditing.Insert(input, 500.0, 0.0);

        await Assert.That(input.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Insert_NegativeCurveLength_BecomesZero()
    {
        var result = ProfileEditing.Insert(new[] { V(0, 100), V(1000, 120) }, 500.0, -5.0);

        await Assert.That(result[1].CurveLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task Insert_OutsideTheAlignmentOrOnAnExistingStation_Throws()
    {
        var vertices = new[] { V(0, 100), V(400, 110), V(1000, 120) };

        await Assert.That(Catch(() => ProfileEditing.Insert(vertices, -10.0, 0.0)) is InvalidOperationException).IsTrue();
        await Assert.That(Catch(() => ProfileEditing.Insert(vertices, 0.0, 0.0)) is InvalidOperationException).IsTrue();
        await Assert.That(Catch(() => ProfileEditing.Insert(vertices, 1000.0, 0.0)) is InvalidOperationException).IsTrue();
        await Assert.That(Catch(() => ProfileEditing.Insert(vertices, 1200.0, 0.0)) is InvalidOperationException).IsTrue();
        await Assert.That(Catch(() => ProfileEditing.Insert(vertices, 400.0, 0.0)) is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task ElevationAt_InterpolatesAndClampsAtTheEnds()
    {
        var vertices = new[] { V(0, 100), V(500, 110), V(1000, 90) };

        await Assert.That(ProfileEditing.ElevationAt(vertices, 250.0)).IsEqualTo(105.0).Within(1e-9);
        await Assert.That(ProfileEditing.ElevationAt(vertices, 750.0)).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(ProfileEditing.ElevationAt(vertices, -50.0)).IsEqualTo(100.0);
        await Assert.That(ProfileEditing.ElevationAt(vertices, 5000.0)).IsEqualTo(90.0);
    }

    [Test]
    public async Task Validate_GoodProfile_HasNoErrors()
    {
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100), V(500, 110, 40), V(1000, 120) }, 1000.0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_ReportsEachKindOfProblem()
    {
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100) }, 1000.0).Count).IsEqualTo(1);
        await Assert.That(ProfileEditing.Validate(null, 1000.0).Count).IsEqualTo(1);
        await Assert.That(ProfileEditing.Validate(new[] { V(5, 100), V(1000, 100) }, 1000.0).Any(e => e.Contains("first"))).IsTrue();
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100), V(900, 100) }, 1000.0).Any(e => e.Contains("last"))).IsTrue();
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100), V(600, 100), V(300, 100), V(1000, 100) }, 1000.0).Any(e => e.Contains("order"))).IsTrue();
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100), V(500, 100, -1), V(1000, 100) }, 1000.0).Any(e => e.Contains("negative"))).IsTrue();
        await Assert.That(ProfileEditing.Validate(new[] { V(0, 100), V(500, double.NaN), V(1000, 100) }, 1000.0).Any(e => e.Contains("not a number"))).IsTrue();
    }

    [Test]
    public async Task Grades_AreRiseOverRun()
    {
        var vertices = new[] { V(0, 100), V(100, 105), V(300, 95) };

        await Assert.That(ProfileEditing.Grade(vertices[0], vertices[1])).IsEqualTo(0.05).Within(1e-12);
        await Assert.That(ProfileEditing.Grade(vertices[1], vertices[2])).IsEqualTo(-0.05).Within(1e-12);
        await Assert.That(ProfileEditing.MaxAbsGrade(vertices)).IsEqualTo(0.05).Within(1e-12);
    }

    [Test]
    public async Task EffectiveCurveLength_IsClampedToHalfTheNeighbouringStraights()
    {
        // 100 ft to the previous station, 600 ft to the next: a 400 ft curve would need 200 ft either side, but a curve may use at most half (50 ft) of the 100 ft straight before it
        var vertices = new[] { V(0, 100), V(100, 110, 400), V(700, 100) };

        await Assert.That(ProfileEditing.EffectiveCurveLength(vertices, 1)).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(ProfileEditing.EffectiveCurveLength(vertices, 0)).IsEqualTo(0.0);
        await Assert.That(ProfileEditing.EffectiveCurveLength(vertices, 2)).IsEqualTo(0.0);
    }

    [Test]
    public async Task EffectiveCurveLength_NoChangeOfGrade_IsZero()
    {
        var vertices = new[] { V(0, 100), V(500, 110, 80), V(1000, 120) };

        await Assert.That(ProfileEditing.EffectiveCurveLength(vertices, 1)).IsEqualTo(0.0);
    }

    [Test]
    public async Task EffectiveCurveLength_MatchesTheCurveThatIsWritten()
    {
        var vertices = new[] { V(0, 100), V(100, 110, 400), V(Length, 100) };
        var document = AlignmentJsonDocument.Parse(Document());

        document.SetProfile(vertices);
        var written = document.ReadProfile();

        await Assert.That(written[1].CurveLength).IsEqualTo(ProfileEditing.EffectiveCurveLength(vertices, 1)).Within(1e-9);
    }
}
