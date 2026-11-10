using static TopoAlign.Tests.TestKit;
using System.Globalization;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Plan;
using TopoAlign.Alignments.Profile;

namespace TopoAlign.Tests;

/// <summary>
/// Reading an alignment's points of intersection back out of its stored plan, and writing an edited plan back. Needs no Revit model.
/// </summary>
public class PiRecoveryTests : TopoAlignTest
{
    private const string LineType = "autodesk.infra.geometry:line2d-1.0.0";
    private const string CircleType = "autodesk.infra.geometry:circle2d-1.0.0";

    // ---- a minimal alignment document ---------------------------------------------------------------------------

    private static string F(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.IndexOfAny(new[] { '.', 'E', 'e' }) < 0 ? text + ".0" : text;
    }

    private static string Point(double x, double y) => $"{{\"Float64\":{{\"x\":{F(x)},\"y\":{F(y)}}}}}";

    private static string Range(double high, int type) =>
        $"{{\"Float64\":{{\"high\":{F(high)},\"low\":0.0}},\"enum<Enum>\":{{\"type\":{type}}}}}";

    private static string Line(double x, double y, double dx, double dy) =>
        $"{{\"autodesk.math:point2d-1.0.0\":{{\"position\":{Point(x, y)}}},\"autodesk.math:vector2d-1.0.0\":{{\"direction\":{Point(dx, dy)}}}," +
        $"\"String\":{{\"guid\":\"g\"}},\"autodesk.geometry:paramrange-1.0.0\":{{\"range\":{Range(0, 0)}}},\"typeid\":\"{LineType}\"}}";

    private static string Circle(double cx, double cy, double rx, double ry, double turns, bool clockwise) =>
        $"{{\"Bool\":{{\"clockwise\":{(clockwise ? "true" : "false")}}},\"autodesk.math:point2d-1.0.0\":{{\"center\":{Point(cx, cy)}}}," +
        $"\"autodesk.math:vector2d-1.0.0\":{{\"radius\":{Point(rx, ry)}}},\"String\":{{\"guid\":\"g\"}}," +
        $"\"autodesk.geometry:paramrange-1.0.0\":{{\"range\":{Range(turns, 1)}}},\"typeid\":\"{CircleType}\"}}";

    private static string Geometry(string curves) =>
        $"{{\"String\":{{\"tag\":\"\"}},\"array<autodesk.infra.geometry:curve2d-1.0.0>\":{{\"curves\":[{curves}]}}," +
        $"\"autodesk.geometry:paramrange-1.0.0\":{{\"range\":{Range(0, 0)}}},\"Spirals\":{{\"Spirals\":[]}}}}";

    private static string Document(string horizontal, string profile) =>
        "{\"String\":{\"guid\":\"root-guid\"},\"autodesk.infra:aecalignmentdata-1.0.0\":{\"Component\":{\"Float64\":{\"CoordSysUnitScaleToMeter\":0.3048},\"String\":{\"CoordSysWKT\":\"\"}}}," +
        "\"autodesk.infra:alignment-1.0.0\":{\"CoreComponent\":{\"Bool\":{\"chordBasedStations\":false}," +
        "\"Float64\":{\"startstation\":0.0,\"unitChordLen\":100.0}," +
        "\"String\":{\"createdOn\":\"\",\"description\":\"\",\"guid\":\"core-guid\",\"lastEditedOn\":\"\",\"name\":\"Road\",\"source\":\"test\"}," +
        "\"autodesk.infra.geometry:curvegeometry-1.0.0\":{\"curveGeometry\":{" +
        $"\"autodesk.infra:horizontalcurvegeometry2d-1.0.0\":{{\"horizontalGeometry\":{Geometry(horizontal)}}}," +
        $"\"autodesk.infra:profilecurvegeometry2d-1.0.0\":{{\"profileGeometry\":{Geometry(profile)}}}}}}}}}}}}}";

    /// <summary>An alignment made by laying the PIs out and writing the result, as Create Alignment does, with a straight falling profile.</summary>
    private static AlignmentJsonDocument Made(params PiPoint[] pis)
    {
        var layout = PiGeometry.Create(pis);
        if (!layout.IsValid)
        {
            throw new InvalidOperationException(layout.Errors[0]);
        }

        // a 10 ft straight with a one-straight profile falling 1 in 10; SetPlan extends that profile to the laid-out length
        var document = AlignmentJsonDocument.Parse(Document(Line(0, 0, 10, 0), Line(0, 10, 10, -1)));
        document.SetPlan(layout.Segments);
        return document;
    }

    private static string HorizontalOf(AlignmentJsonDocument document)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(document.ToJson())!;
        var curves = (System.Text.Json.Nodes.JsonArray)node["autodesk.infra:alignment-1.0.0"]!["CoreComponent"]!
            ["autodesk.infra.geometry:curvegeometry-1.0.0"]!["curveGeometry"]!["autodesk.infra:horizontalcurvegeometry2d-1.0.0"]!
            ["horizontalGeometry"]!["array<autodesk.infra.geometry:curve2d-1.0.0>"]!["curves"]!;
        return string.Join(",", curves.Select(c => c!.ToJsonString()));
    }

    private static List<PiPoint> Recover(AlignmentJsonDocument document)
    {
        var ok = PiRecovery.TryRecover(document.ReadPlan(), out var pis, out var reason);
        if (!ok)
        {
            throw new InvalidOperationException(reason);
        }

        return pis;
    }

    // ---- recovering points --------------------------------------------------------------------------------------

    [Test]
    public async Task Recover_SharpCorners_ComeBackAsPoints()
    {
        var pis = Recover(Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 0, 0), new PiPoint(400, 300, 0, 0)));

        await Assert.That(pis.Count).IsEqualTo(3);
        await Assert.That(pis[1].X).IsEqualTo(400.0).Within(0.001);
        await Assert.That(pis[1].Y).IsEqualTo(0.0).Within(0.001);
        await Assert.That(pis[1].Radius).IsEqualTo(0.0);
    }

    [Test]
    public async Task Recover_ArcOnly_GivesRadiusAndNoSpiral()
    {
        var pis = Recover(Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 100, 0), new PiPoint(400, 300, 0, 0)));

        await Assert.That(pis.Count).IsEqualTo(3);
        await Assert.That(pis[1].X).IsEqualTo(400.0).Within(0.001);
        await Assert.That(pis[1].Y).IsEqualTo(0.0).Within(0.001);
        await Assert.That(pis[1].Radius).IsEqualTo(100.0).Within(0.001);
        await Assert.That(pis[1].SpiralLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task Recover_Spirals_GivesRadiusAndSpiralLength()
    {
        var pis = Recover(Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 150, 60), new PiPoint(700, 300, 120, 40), new PiPoint(1000, 300, 0, 0)));

        await Assert.That(pis.Count).IsEqualTo(4);
        await Assert.That(pis[1].X).IsEqualTo(400.0).Within(0.01);
        await Assert.That(pis[1].Radius).IsEqualTo(150.0).Within(0.01);
        await Assert.That(pis[1].SpiralLength).IsEqualTo(60.0).Within(0.05);
        await Assert.That(pis[2].X).IsEqualTo(700.0).Within(0.01);
        await Assert.That(pis[2].Y).IsEqualTo(300.0).Within(0.01);
        await Assert.That(pis[2].Radius).IsEqualTo(120.0).Within(0.01);
        await Assert.That(pis[2].SpiralLength).IsEqualTo(40.0).Within(0.05);
    }

    [Test]
    public async Task Recover_RightHandCurve_Works()
    {
        var pis = Recover(Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 100, 30), new PiPoint(600, -300, 0, 0)));

        await Assert.That(pis.Count).IsEqualTo(3);
        await Assert.That(pis[1].Radius).IsEqualTo(100.0).Within(0.01);
        await Assert.That(pis[1].SpiralLength).IsEqualTo(30.0).Within(0.05);
    }

    [Test]
    public async Task Recover_MixedSharpAndCurvedPoints_KeepsEveryPoint()
    {
        var pis = Recover(Made(new PiPoint(0, 0, 0, 0), new PiPoint(300, 0, 0, 0), new PiPoint(600, 200, 120, 40),
            new PiPoint(900, 200, 0, 0), new PiPoint(900, 500, 0, 0)));

        await Assert.That(pis.Count).IsEqualTo(5);
        await Assert.That(pis[1].Radius).IsEqualTo(0.0);
        await Assert.That(pis[2].Radius).IsEqualTo(120.0).Within(0.01);
        await Assert.That(pis[3].Radius).IsEqualTo(0.0);
    }

    [Test]
    public async Task Recover_ThenLayOut_ReproducesTheStoredLength()
    {
        var document = Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 150, 60), new PiPoint(700, 300, 120, 40), new PiPoint(1000, 300, 0, 0));
        var layout = PiGeometry.Create(Recover(document));

        await Assert.That(layout.IsValid).IsTrue();
        await Assert.That(PlanSegment.TotalLength(layout.Segments)).IsEqualTo(document.PlanLength).Within(0.1);
    }

    [Test]
    public async Task Recover_PlanThatStartsWithAnArc_IsRefusedWithAReason()
    {
        var document = AlignmentJsonDocument.Parse(Document(Circle(0, 50, 0, -50, 0.25, false) + "," + Line(50, 50, 0, 100), Line(0, 10, 100, 0)));

        var ok = PiRecovery.TryRecover(document.ReadPlan(), out _, out var reason);

        await Assert.That(ok).IsFalse();
        await Assert.That(string.IsNullOrEmpty(reason)).IsFalse();
    }

    [Test]
    public async Task Recover_TwoArcsInARow_IsRefused()
    {
        var document = AlignmentJsonDocument.Parse(Document(
            Line(0, 0, 100, 0) + "," + Circle(100, 50, 0, -50, 0.125, false) + "," + Circle(100, 50, 35.35533905932738, -35.35533905932738, 0.125, false) + "," + Line(100, 100, -100, 0),
            Line(0, 10, 100, 0)));

        var ok = PiRecovery.TryRecover(document.ReadPlan(), out _, out var reason);

        await Assert.That(ok).IsFalse();
        await Assert.That(string.IsNullOrEmpty(reason)).IsFalse();
    }

    // ---- writing an edited plan ---------------------------------------------------------------------------------

    [Test]
    public async Task SetPlan_MovedPoint_ChangesThePlanAndKeepsTheGuids()
    {
        var document = Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 100, 0), new PiPoint(400, 300, 0, 0));
        var pis = Recover(document);

        var moved = new List<PiPoint> { pis[0], new PiPoint(450, 0, pis[1].Radius, pis[1].SpiralLength), new PiPoint(450, 300, 0, 0) };
        var layout = PiGeometry.Create(moved);
        var length = document.SetPlan(layout.Segments);

        var plan = document.ReadPlan();
        await Assert.That(length).IsEqualTo(document.PlanLength).Within(1e-6);
        await Assert.That(plan[plan.Count - 1].EndPoint.X).IsEqualTo(450.0).Within(0.001);
        await Assert.That(plan[plan.Count - 1].EndPoint.Y).IsEqualTo(300.0).Within(0.001);

        var json = document.ToJson();
        await Assert.That(json.Contains("root-guid")).IsTrue();
        await Assert.That(json.Contains("core-guid")).IsTrue();
    }

    [Test]
    public async Task SetPlan_LongerAlignment_ExtendsTheLastGradeOfTheProfile()
    {
        var document = Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 0, 0));
        var before = document.ReadProfile();

        var longer = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(600, 0, 0, 0) });
        var length = document.SetPlan(longer.Segments);

        var after = document.ReadProfile();
        await Assert.That(length).IsEqualTo(600.0).Within(1e-6);
        await Assert.That(after[after.Count - 1].Distance).IsEqualTo(600.0).Within(1e-6);
        await Assert.That(after[0].Elevation).IsEqualTo(before[0].Elevation);

        // the grade of the last straight is unchanged
        var gradeBefore = ProfileEditing.Grade(before[before.Count - 2], before[before.Count - 1]);
        var gradeAfter = ProfileEditing.Grade(after[after.Count - 2], after[after.Count - 1]);
        await Assert.That(gradeAfter).IsEqualTo(gradeBefore).Within(1e-9);
    }

    [Test]
    public async Task SetPlan_EndsBeforeTheLastProfilePoint_IsRefusedAndLeavesTheDocumentAsItWas()
    {
        var document = Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 0, 0));
        var profile = Document(HorizontalOf(document), Line(0, 10, 100, -5) + "," + Line(100, 5, 300, 0));
        var edited = AlignmentJsonDocument.Parse(profile);
        var before = edited.ToJson();

        // the profile's middle station is at 100, so a 50 ft alignment ends before the last straight starts
        var shorter = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(50, 0, 0, 0) });

        var threw = false;
        try
        {
            edited.SetPlan(shorter.Segments);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
        await Assert.That(edited.ToJson()).IsEqualTo(before);
    }

    [Test]
    public async Task SetPlan_WritesEveryNumberWithADecimalPoint()
    {
        var document = Made(new PiPoint(0, 0, 0, 0), new PiPoint(400, 0, 100, 30), new PiPoint(400, 300, 0, 0));
        var json = document.ToJson();

        await Assert.That(json.Contains("\"x\":400,")).IsFalse();
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(json, "\"(x|y|high|low)\":-?\\d+[,}]")).IsFalse();
    }
}
