using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Text.Json.Nodes;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;

namespace TopoAlign.Tests;

public class AlignmentJsonBuilderTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    private static JsonObject Build(IReadOnlyList<Curve> curves, string name = "Test", Transform transform = null, double startStation = 0.0)
    {
        return Parse(AlignmentJsonBuilder.Build(name, curves, transform, startStation, out _));
    }

    /// <summary>Names of numeric values written without a decimal point (enum values are allowed to be integers).</summary>
    private static List<string> IntegerFormattedNumbers(string json)
    {
        var offenders = new List<string>();
        var reader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        var property = string.Empty;

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

        return offenders;
    }

    // ---- document structure -------------------------------------------------------------------------------------

    [Test]
    public async Task Build_ProducesValidJsonObject()
    {
        var json = AlignmentJsonBuilder.Build("Test", new Curve[] { Ln(0, 0, 10, 0) }, Transform.Identity, 0.0, out _);

        var root = JsonNode.Parse(json);

        await Assert.That(root is JsonObject).IsTrue();
    }

    [Test]
    public async Task Build_WritesRootGuid()
    {
        var root = Build(new Curve[] { Ln(0, 0, 10, 0) });

        var guid = root["String"]!["guid"]!.GetValue<string>();

        await Assert.That(Guid.TryParse(guid, out _)).IsTrue();
    }

    [Test]
    public async Task Build_EachCallGetsNewGuids()
    {
        var curves = new Curve[] { Ln(0, 0, 10, 0) };

        var first = Build(curves);
        var second = Build(curves);

        await Assert.That(first["String"]!["guid"]!.GetValue<string>()).IsNotEqualTo(second["String"]!["guid"]!.GetValue<string>());
        await Assert.That(CoreComponent(first)["String"]!["guid"]!.GetValue<string>())
            .IsNotEqualTo(CoreComponent(second)["String"]!["guid"]!.GetValue<string>());
    }

    [Test]
    public async Task Build_UsesFeetUnitScale()
    {
        var root = Build(new Curve[] { Ln(0, 0, 10, 0) });

        var component = root[AecAlignmentDataKey]!["Component"]!;

        await Assert.That(component["Float64"]!["CoordSysUnitScaleToMeter"]!.GetValue<double>()).IsEqualTo(0.3048).Within(1e-12);
        await Assert.That(component["String"]!["CoordSysWKT"]!.GetValue<string>()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Build_WritesCoreComponentProperties()
    {
        var core = CoreComponent(Build(new Curve[] { Ln(0, 0, 10, 0) }, "My Road"));

        await Assert.That(core["Bool"]!["chordBasedStations"]!.GetValue<bool>()).IsFalse();
        await Assert.That(core["Float64"]!["startstation"]!.GetValue<double>()).IsEqualTo(0.0);
        await Assert.That(core["Float64"]!["unitChordLen"]!.GetValue<double>()).IsEqualTo(100.0);
        await Assert.That(core["String"]!["name"]!.GetValue<string>()).IsEqualTo("My Road");
        await Assert.That(core["String"]!["source"]!.GetValue<string>()).IsEqualTo("TopoAlign");
        await Assert.That(core["String"]!["description"]!.GetValue<string>()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Build_NullName_WritesEmptyName()
    {
        var core = CoreComponent(Build(new Curve[] { Ln(0, 0, 10, 0) }, name: null));

        await Assert.That(core["String"]!["name"]!.GetValue<string>()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Build_GeometryBlocksCarryTagRangeAndEmptySpirals()
    {
        var root = Build(new Curve[] { Ln(0, 0, 10, 0) });

        foreach (var geometry in new[] { HorizontalGeometry(root), ProfileGeometry(root) })
        {
            await Assert.That(geometry["String"]!["tag"]!.GetValue<string>()).IsEqualTo(string.Empty);
            await Assert.That(((JsonArray)geometry["Spirals"]!["Spirals"]!).Count).IsEqualTo(0);
            var range = geometry[RangeKey]!["range"]!;
            await Assert.That(range["Float64"]!["low"]!.GetValue<double>()).IsEqualTo(0.0);
            await Assert.That(range["Float64"]!["high"]!.GetValue<double>()).IsEqualTo(0.0);
            await Assert.That(range["enum<Enum>"]!["type"]!.GetValue<int>()).IsEqualTo(0);
        }
    }

    // ---- lines --------------------------------------------------------------------------------------------------

    [Test]
    public async Task Build_Line_WritesPositionAndDirectionVector()
    {
        var curves = Curves(HorizontalGeometry(Build(new Curve[] { Ln(2, 3, 12, 3) })));

        await Assert.That(curves.Count).IsEqualTo(1);
        var line = curves[0]!;
        await Assert.That(TypeId(line)).IsEqualTo(LineTypeId);
        var position = PointOf(line, Point2DKey, "position");
        var direction = PointOf(line, Vector2DKey, "direction");
        await Assert.That(position.X).IsEqualTo(2.0).Within(Tol);
        await Assert.That(position.Y).IsEqualTo(3.0).Within(Tol);
        await Assert.That(direction.X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(direction.Y).IsEqualTo(0.0).Within(Tol);
        var range = RangeOf(line);
        await Assert.That(range.Type).IsEqualTo(0);
        await Assert.That(range.High).IsEqualTo(0.0);
        await Assert.That(line["String"]!["guid"]!.GetValue<string>().Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Build_DiagonalLine_DirectionIsEndMinusStart()
    {
        var line = Curves(HorizontalGeometry(Build(new Curve[] { Ln(1, 1, -2, 5) })))[0]!;

        var direction = PointOf(line, Vector2DKey, "direction");

        await Assert.That(direction.X).IsEqualTo(-3.0).Within(Tol);
        await Assert.That(direction.Y).IsEqualTo(4.0).Within(Tol);
    }

    [Test]
    public async Task Build_LineElevationIsDropped_FromHorizontalGeometry()
    {
        var line = Curves(HorizontalGeometry(Build(new Curve[] { Ln(0, 0, 3, 4, 10) })))[0]!;

        var direction = PointOf(line, Vector2DKey, "direction");

        await Assert.That(Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y)).IsEqualTo(5.0).Within(Tol);
    }

    [Test]
    public async Task Build_MultipleCurves_KeepOrderAndGetDistinctGuids()
    {
        var curves = Curves(HorizontalGeometry(Build(new Curve[] { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 20, 10) })));

        await Assert.That(curves.Count).IsEqualTo(3);
        await Assert.That(PointOf(curves[0]!, Point2DKey, "position").X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(PointOf(curves[1]!, Point2DKey, "position").X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(PointOf(curves[2]!, Point2DKey, "position").Y).IsEqualTo(10.0).Within(Tol);
        var guids = curves.Select(c => c!["String"]!["guid"]!.GetValue<string>()).Distinct().Count();
        await Assert.That(guids).IsEqualTo(3);
    }

    // ---- arcs ---------------------------------------------------------------------------------------------------

    [Test]
    public async Task Build_CounterClockwiseQuarterArc_WritesCircle()
    {
        var circle = Curves(HorizontalGeometry(Build(new Curve[] { QuarterArcCcw(10) })))[0]!;

        await Assert.That(TypeId(circle)).IsEqualTo(CircleTypeId);
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
        var center = PointOf(circle, Point2DKey, "center");
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(center.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(center.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(radius.X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(radius.Y).IsEqualTo(0.0).Within(Tol);
        var range = RangeOf(circle);
        await Assert.That(range.Type).IsEqualTo(1);
        await Assert.That(range.Low).IsEqualTo(0.0);
        await Assert.That(range.High).IsEqualTo(0.25).Within(Tol);
    }

    [Test]
    public async Task Build_ClockwiseQuarterArc_SetsClockwiseFlag()
    {
        var circle = Curves(HorizontalGeometry(Build(new Curve[] { QuarterArcCw(10) })))[0]!;

        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsTrue();
        await Assert.That(RangeOf(circle).High).IsEqualTo(0.25).Within(Tol);
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(radius.X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(radius.Y).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task Build_ReversedArc_SwapsDirectionAndStartVector()
    {
        var reversed = (Arc)Reversed(QuarterArcCcw(10));

        var circle = Curves(HorizontalGeometry(Build(new Curve[] { reversed })))[0]!;

        // The reversed arc starts at (0, 10) and runs clockwise to (10, 0).
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsTrue();
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(radius.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(radius.Y).IsEqualTo(10.0).Within(Tol);
        await Assert.That(RangeOf(circle).High).IsEqualTo(0.25).Within(Tol);
    }

    [Test]
    public async Task Build_LargeArc_SweepIsMoreThanHalfATurn()
    {
        // 270 degrees counter-clockwise from (10, 0) through (-7.07, 7.07) to (0, -10).
        var d = 10 * Math.Sqrt(0.5);
        var arc = ArcThrough(10, 0, -d, d, 0, -10);

        var circle = Curves(HorizontalGeometry(Build(new Curve[] { arc })))[0]!;

        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
        await Assert.That(RangeOf(circle).High).IsEqualTo(0.75).Within(Tol);
    }

    [Test]
    public async Task Build_ArcAwayFromOrigin_UsesArcCentre()
    {
        var arc = ArcThrough(20, 5, 15 + 5 * Math.Sqrt(0.5), 5 + 5 * Math.Sqrt(0.5), 15, 10);

        var circle = Curves(HorizontalGeometry(Build(new Curve[] { arc })))[0]!;

        var center = PointOf(circle, Point2DKey, "center");
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(center.X).IsEqualTo(15.0).Within(Tol);
        await Assert.That(center.Y).IsEqualTo(5.0).Within(Tol);
        await Assert.That(radius.X).IsEqualTo(5.0).Within(Tol);
        await Assert.That(radius.Y).IsEqualTo(0.0).Within(Tol);
    }

    // ---- profile ------------------------------------------------------------------------------------------------

    [Test]
    public async Task Build_Profile_IsFlatLineAsLongAsTheHorizontalAlignment()
    {
        var profile = Curves(ProfileGeometry(Build(new Curve[] { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10) })));

        await Assert.That(profile.Count).IsEqualTo(1);
        var line = profile[0]!;
        await Assert.That(TypeId(line)).IsEqualTo(LineTypeId);
        var position = PointOf(line, Point2DKey, "position");
        var direction = PointOf(line, Vector2DKey, "direction");
        await Assert.That(position.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(position.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(direction.X).IsEqualTo(20.0).Within(Tol);
        await Assert.That(direction.Y).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task Build_Profile_LengthIncludesArcLength()
    {
        var profile = Curves(ProfileGeometry(Build(new Curve[] { Ln(30, 0, 10, 0), QuarterArcCcw(10) })));

        var direction = PointOf(profile[0]!, Vector2DKey, "direction");

        await Assert.That(direction.X).IsEqualTo(20.0 + 5.0 * Math.PI).Within(1e-4);
    }

    [Test]
    public async Task Build_Profile_UsesElevationOfFirstCurve()
    {
        var profile = Curves(ProfileGeometry(Build(new Curve[] { Ln(0, 0, 10, 0, 7.5) })));

        var position = PointOf(profile[0]!, Point2DKey, "position");

        await Assert.That(position.Y).IsEqualTo(7.5).Within(Tol);
    }

    // ---- transform ----------------------------------------------------------------------------------------------

    [Test]
    public async Task Build_NullTransform_BehavesLikeIdentity()
    {
        var identity = Curves(HorizontalGeometry(Build(new Curve[] { Ln(1, 2, 11, 2) }, transform: Transform.Identity)))[0]!;
        var none = Curves(HorizontalGeometry(Build(new Curve[] { Ln(1, 2, 11, 2) }, transform: null)))[0]!;

        await Assert.That(PointOf(none, Point2DKey, "position")).IsEqualTo(PointOf(identity, Point2DKey, "position"));
        await Assert.That(PointOf(none, Vector2DKey, "direction")).IsEqualTo(PointOf(identity, Vector2DKey, "direction"));
    }

    [Test]
    public async Task Build_Translation_MovesPositionAndProfileElevation()
    {
        var transform = Transform.CreateTranslation(new XYZ(100, 200, 30));

        var root = Build(new Curve[] { Ln(0, 0, 10, 0, 5) }, transform: transform);

        var line = Curves(HorizontalGeometry(root))[0]!;
        var position = PointOf(line, Point2DKey, "position");
        var direction = PointOf(line, Vector2DKey, "direction");
        await Assert.That(position.X).IsEqualTo(100.0).Within(Tol);
        await Assert.That(position.Y).IsEqualTo(200.0).Within(Tol);
        await Assert.That(direction.X).IsEqualTo(10.0).Within(Tol);
        var profile = Curves(ProfileGeometry(root))[0]!;
        await Assert.That(PointOf(profile, Point2DKey, "position").Y).IsEqualTo(35.0).Within(Tol);
    }

    [Test]
    public async Task Build_Rotation_RotatesPositionAndDirection()
    {
        var transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, Math.PI / 2, XYZ.Zero);

        var line = Curves(HorizontalGeometry(Build(new Curve[] { Ln(10, 0, 20, 0) }, transform: transform)))[0]!;

        var position = PointOf(line, Point2DKey, "position");
        var direction = PointOf(line, Vector2DKey, "direction");
        await Assert.That(position.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(position.Y).IsEqualTo(10.0).Within(Tol);
        await Assert.That(direction.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(direction.Y).IsEqualTo(10.0).Within(Tol);
    }

    [Test]
    public async Task Build_Rotation_KeepsArcSweepAndDirection()
    {
        var transform = Transform.CreateRotationAtPoint(XYZ.BasisZ, Math.PI / 2, XYZ.Zero);

        var circle = Curves(HorizontalGeometry(Build(new Curve[] { QuarterArcCcw(10) }, transform: transform)))[0]!;

        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
        await Assert.That(RangeOf(circle).High).IsEqualTo(0.25).Within(Tol);
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(radius.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(radius.Y).IsEqualTo(10.0).Within(Tol);
    }

    // ---- start station, plan length and number format -----------------------------------------------------------

    [Test]
    public async Task Build_StartStation_IsWrittenToTheCoreComponent()
    {
        var core = CoreComponent(Build(new Curve[] { Ln(0, 0, 10, 0) }, startStation: 1250.5));

        await Assert.That(core["Float64"]!["startstation"]!.GetValue<double>()).IsEqualTo(1250.5).Within(1e-9);
    }

    [Test]
    public async Task Build_PlanLength_IsTheSumOfThePlanLengths()
    {
        AlignmentJsonBuilder.Build("T", new Curve[] { Ln(0, 0, 3, 4, 10), Ln(3, 4, 3, 14) }, Transform.Identity, 0.0, out var lines);
        AlignmentJsonBuilder.Build("T", new Curve[] { Ln(30, 0, 10, 0), QuarterArcCcw(10) }, Transform.Identity, 0.0, out var withArc);

        await Assert.That(lines).IsEqualTo(15.0).Within(1e-6);
        await Assert.That(withArc).IsEqualTo(20.0 + 5.0 * Math.PI).Within(1e-4);
    }

    [Test]
    public async Task Build_PlanLength_MatchesTheProfileLine()
    {
        var json = AlignmentJsonBuilder.Build("T", new Curve[] { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10) }, Transform.Identity, 0.0, out var length);

        var profile = Curves(ProfileGeometry(Parse(json)))[0]!;

        await Assert.That(PointOf(profile, Vector2DKey, "direction").X).IsEqualTo(length).Within(1e-9);
    }

    [Test]
    public async Task Build_WritesEveryFloatWithADecimalPoint()
    {
        // Autodesk's parser rejects whole numbers written as integers, so 100.0 must not become 100.
        var json = AlignmentJsonBuilder.Build("T",
            new Curve[] { Ln(0, 0, 100, 0), Ln(100, 0, 100, 50), QuarterArcCcw(10) }, Transform.Identity, 100.0, out _);

        await Assert.That(IntegerFormattedNumbers(json).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Build_WholeNumberValues_KeepTheirDecimalPoint()
    {
        var json = AlignmentJsonBuilder.Build("T", new Curve[] { Ln(0, 0, 100, 0) }, Transform.Identity, 0.0, out _);

        await Assert.That(json.Contains("\"startstation\":0.0")).IsTrue();
        await Assert.That(json.Contains("\"unitChordLen\":100.0")).IsTrue();
    }

    // ---- degenerate and invalid input ---------------------------------------------------------------------------

    [Test]
    public async Task Build_OnlyVerticalCurves_ThrowsBecauseTheyHaveNoPlanLength()
    {
        var exception = Catch(() => AlignmentJsonBuilder.Build("T",
            new Curve[] { Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 0, 5)) }, Transform.Identity, 0.0, out _));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task Build_VerticalLineBetweenRealCurves_IsSkipped()
    {
        var vertical = Line.CreateBound(new XYZ(10, 0, 0), new XYZ(10, 0, 5));

        var curves = Curves(HorizontalGeometry(Build(new Curve[] { Ln(0, 0, 10, 0), vertical, Ln(10, 0, 20, 0) })));

        await Assert.That(curves.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Build_NoCurves_Throws()
    {
        var empty = Catch(() => AlignmentJsonBuilder.Build("T", new List<Curve>(), Transform.Identity, 0.0, out _));
        var none = Catch(() => AlignmentJsonBuilder.Build("T", null, Transform.Identity, 0.0, out _));

        await Assert.That(empty is ArgumentException).IsTrue();
        await Assert.That(none is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Build_UnsupportedCurve_Throws()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        var exception = Catch(() => AlignmentJsonBuilder.Build("T", new Curve[] { ellipse }, Transform.Identity, 0.0, out _));

        await Assert.That(exception is NotSupportedException).IsTrue();
    }
}
