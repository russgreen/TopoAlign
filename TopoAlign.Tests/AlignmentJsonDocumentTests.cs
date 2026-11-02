using static TopoAlign.Tests.TestKit;
using System.Globalization;
using System.Text.Json.Nodes;
using TopoAlign.Alignments;

namespace TopoAlign.Tests;

/// <summary>Editing existing infra alignment JSON: name, start station and direction. Needs no Revit model.</summary>
public class AlignmentJsonDocumentTests : TopoAlignTest
{
    private const string LineType = "autodesk.infra.geometry:line2d-1.0.0";
    private const string CircleType = "autodesk.infra.geometry:circle2d-1.0.0";
    private const string VerticalType = "autodesk.infra.geometry:verticalCurve-1.0.0";

    private static readonly double ArcLength = Math.PI * 50.0 / 2.0;
    private static readonly double Length = 100.0 + ArcLength + 100.0;

    // ---- a small alignment written the way the add-in stores it ---------------------------------------------------

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

    private static string Vertical(double sx, double sy, double px, double py, double ex, double ey) =>
        $"{{\"autodesk.math:point2d-1.0.0\":{{\"endPoint\":{Point(ex, ey)},\"pvi\":{Point(px, py)},\"startPoint\":{Point(sx, sy)}}}," +
        $"\"String\":{{\"guid\":\"g\"}},\"autodesk.geometry:paramrange-1.0.0\":{{\"range\":{Range(0, 0)}}},\"typeid\":\"{VerticalType}\"}}";

    private static string Geometry(string curves) =>
        $"{{\"String\":{{\"tag\":\"\"}},\"array<autodesk.infra.geometry:curve2d-1.0.0>\":{{\"curves\":[{curves}]}}," +
        $"\"autodesk.geometry:paramrange-1.0.0\":{{\"range\":{Range(0, 0)}}},\"Spirals\":{{\"Spirals\":[]}}}}";

    private static string Document(string horizontal, string profile, string name = "Road", double startStation = 0.0) =>
        "{\"String\":{\"guid\":\"root-guid\"},\"autodesk.infra:aecalignmentdata-1.0.0\":{\"Component\":{\"Float64\":{\"CoordSysUnitScaleToMeter\":0.3048},\"String\":{\"CoordSysWKT\":\"\"}}}," +
        "\"autodesk.infra:alignment-1.0.0\":{\"CoreComponent\":{\"Bool\":{\"chordBasedStations\":false}," +
        $"\"Float64\":{{\"startstation\":{F(startStation)},\"unitChordLen\":100.0}}," +
        $"\"String\":{{\"createdOn\":\"\",\"description\":\"\",\"guid\":\"core-guid\",\"lastEditedOn\":\"\",\"name\":\"{name}\",\"source\":\"test\"}}," +
        "\"autodesk.infra.geometry:curvegeometry-1.0.0\":{\"curveGeometry\":{" +
        $"\"autodesk.infra:horizontalcurvegeometry2d-1.0.0\":{{\"horizontalGeometry\":{Geometry(horizontal)}}}," +
        $"\"autodesk.infra:profilecurvegeometry2d-1.0.0\":{{\"profileGeometry\":{Geometry(profile)}}}}}}}}}}}}}";

    /// <summary>100 ft east, a counter-clockwise quarter turn of radius 50, then 100 ft north.</summary>
    private static string Sample(string name = "Road", double startStation = 0.0, bool clockwiseArc = false)
    {
        var horizontal = clockwiseArc
            ? Line(0, 0, 100, 0) + "," + Circle(100, -50, 0, 50, 0.25, true) + "," + Line(150, -50, 0, -100)
            : Line(0, 0, 100, 0) + "," + Circle(100, 50, 0, -50, 0.25, false) + "," + Line(150, 50, 0, 100);

        var profile = Line(0, 10, 100, 5) + "," + Vertical(100, 15, 140, 17, 180, 16) + "," + Line(180, 16, Length - 180, -2);

        return Document(horizontal, profile, name, startStation);
    }

    private static JsonArray Horizontal(string json) => Curves(Block(json, "autodesk.infra:horizontalcurvegeometry2d-1.0.0", "horizontalGeometry"));

    private static JsonArray Profile(string json) => Curves(Block(json, "autodesk.infra:profilecurvegeometry2d-1.0.0", "profileGeometry"));

    private static JsonObject Block(string json, string key, string name) =>
        (JsonObject)JsonNode.Parse(json)!["autodesk.infra:alignment-1.0.0"]!["CoreComponent"]!["autodesk.infra.geometry:curvegeometry-1.0.0"]!["curveGeometry"]![key]![name]!;

    private static JsonArray Curves(JsonObject block) => (JsonArray)block["array<autodesk.infra.geometry:curve2d-1.0.0>"]!["curves"]!;

    private static (double X, double Y) At(JsonNode curve, string group, string name)
    {
        var values = curve[group]![name]!["Float64"]!;
        return (values["x"]!.GetValue<double>(), values["y"]!.GetValue<double>());
    }

    private static string Type(JsonNode curve) => curve["typeid"]!.GetValue<string>();

    private static List<string> IntegerLookingFloats(string json)
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

    // ---- reading ------------------------------------------------------------------------------------------------

    [Test]
    public async Task Parse_ReadsNameStartStationAndPlanLength()
    {
        var document = AlignmentJsonDocument.Parse(Sample("Main St", 250.5));

        await Assert.That(document.Name).IsEqualTo("Main St");
        await Assert.That(document.StartStation).IsEqualTo(250.5);
        await Assert.That(document.PlanLength).IsEqualTo(Length).Within(1e-9);
    }

    [Test]
    public async Task Parse_BadInput_IsRejectedWithAReadableMessage()
    {
        foreach (var bad in new[] { null, "", "   ", "not json", "{}", "{\"a\":1}", "[1,2]" })
        {
            var exception = Catch(() => AlignmentJsonDocument.Parse(bad));

            await Assert.That(exception is InvalidOperationException).IsTrue();
            await Assert.That(exception.Message.Length).IsGreaterThan(5);
        }
    }

    [Test]
    public async Task Parse_MissingStartStationAndName_DefaultToZeroAndEmpty()
    {
        var json = "{\"autodesk.infra:alignment-1.0.0\":{\"CoreComponent\":{}}}";

        var document = AlignmentJsonDocument.Parse(json);

        await Assert.That(document.Name).IsEqualTo(string.Empty);
        await Assert.That(document.StartStation).IsEqualTo(0.0);
        await Assert.That(document.PlanLength).IsEqualTo(0.0);
        await Assert.That(document.CanReverse(out _)).IsTrue();
    }

    [Test]
    public async Task ToJson_WithoutEdits_KeepsTheDocumentExactly()
    {
        var json = Sample("Road", 12.0);

        var written = AlignmentJsonDocument.Parse(json).ToJson();

        await Assert.That(written).IsEqualTo(JsonNode.Parse(json)!.ToJsonString());
    }

    // ---- name and start station ---------------------------------------------------------------------------------

    [Test]
    public async Task SetName_ChangesOnlyTheName()
    {
        var document = AlignmentJsonDocument.Parse(Sample("Old"));

        document.Name = "New name";

        var edited = AlignmentJsonDocument.Parse(document.ToJson());
        await Assert.That(edited.Name).IsEqualTo("New name");
        await Assert.That(edited.PlanLength).IsEqualTo(Length).Within(1e-9);
        await Assert.That(document.ToJson().Contains("core-guid")).IsTrue();
    }

    [Test]
    public async Task SetName_Null_BecomesEmpty()
    {
        var document = AlignmentJsonDocument.Parse(Sample("Old"));

        document.Name = null;

        await Assert.That(document.Name).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task SetStartStation_IsWrittenWithADecimalPoint()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.StartStation = 1000;

        await Assert.That(document.ToJson().Contains("\"startstation\":1000.0")).IsTrue();
        await Assert.That(AlignmentJsonDocument.Parse(document.ToJson()).StartStation).IsEqualTo(1000.0);
    }

    [Test]
    public async Task SetStartStation_CanBeNegative()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.StartStation = -42.25;

        await Assert.That(AlignmentJsonDocument.Parse(document.ToJson()).StartStation).IsEqualTo(-42.25);
    }

    [Test]
    public async Task Setters_CreateTheGroupsWhenTheyAreMissing()
    {
        var document = AlignmentJsonDocument.Parse("{\"autodesk.infra:alignment-1.0.0\":{\"CoreComponent\":{}}}");

        document.Name = "Fresh";
        document.StartStation = 5;

        var again = AlignmentJsonDocument.Parse(document.ToJson());
        await Assert.That(again.Name).IsEqualTo("Fresh");
        await Assert.That(again.StartStation).IsEqualTo(5.0);
    }

    // ---- reversing: plan ----------------------------------------------------------------------------------------

    [Test]
    public async Task Reverse_PutsTheCurvesInTheOppositeOrder()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.Reverse();

        var curves = Horizontal(document.ToJson());
        await Assert.That(curves.Count).IsEqualTo(3);
        await Assert.That(Type(curves[0]!)).IsEqualTo(LineType);
        await Assert.That(Type(curves[1]!)).IsEqualTo(CircleType);
        await Assert.That(Type(curves[2]!)).IsEqualTo(LineType);
        // the 100 ft northbound straight (150,50)-(150,150) is now first and runs south
        var first = At(curves[0]!, "autodesk.math:point2d-1.0.0", "position");
        var direction = At(curves[0]!, "autodesk.math:vector2d-1.0.0", "direction");
        await Assert.That(first.X).IsEqualTo(150.0).Within(1e-9);
        await Assert.That(first.Y).IsEqualTo(150.0).Within(1e-9);
        await Assert.That(direction.X).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(direction.Y).IsEqualTo(-100.0).Within(1e-9);
    }

    [Test]
    public async Task Reverse_ArcStartsAtItsOldEndAndSweepsTheOtherWay()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.Reverse();

        var circle = Horizontal(document.ToJson())[1]!;
        var center = At(circle, "autodesk.math:point2d-1.0.0", "center");
        var radius = At(circle, "autodesk.math:vector2d-1.0.0", "radius");
        await Assert.That(center.X).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(center.Y).IsEqualTo(50.0).Within(1e-9);
        // it used to end at (150, 50): east of the centre
        await Assert.That(radius.X).IsEqualTo(50.0).Within(1e-9);
        await Assert.That(radius.Y).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsTrue();
        await Assert.That(circle["autodesk.geometry:paramrange-1.0.0"]!["range"]!["Float64"]!["high"]!.GetValue<double>()).IsEqualTo(0.25).Within(1e-12);
    }

    [Test]
    public async Task Reverse_ClockwiseArc_BecomesCounterClockwise()
    {
        var document = AlignmentJsonDocument.Parse(Sample(clockwiseArc: true));

        document.Reverse();

        var circle = Horizontal(document.ToJson())[1]!;
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
    }

    [Test]
    public async Task Reverse_KeepsTheLengthTheNameTheGuidAndTheStartStation()
    {
        var document = AlignmentJsonDocument.Parse(Sample("Keep me", 77.0));

        document.Reverse();

        var again = AlignmentJsonDocument.Parse(document.ToJson());
        await Assert.That(again.PlanLength).IsEqualTo(Length).Within(1e-9);
        await Assert.That(again.Name).IsEqualTo("Keep me");
        await Assert.That(again.StartStation).IsEqualTo(77.0);
        await Assert.That(document.ToJson().Contains("core-guid")).IsTrue();
        await Assert.That(document.ToJson().Contains("root-guid")).IsTrue();
    }

    // ---- reversing: profile -------------------------------------------------------------------------------------

    [Test]
    public async Task Reverse_MirrorsTheProfileStraights()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.Reverse();

        var profile = Profile(document.ToJson());
        await Assert.That(profile.Count).IsEqualTo(3);
        // the original first straight (0,10) -> (100,15) is now last, running from (L-100, 15) to (L, 10)
        var last = profile[2]!;
        var position = At(last, "autodesk.math:point2d-1.0.0", "position");
        var direction = At(last, "autodesk.math:vector2d-1.0.0", "direction");
        await Assert.That(position.X).IsEqualTo(Length - 100).Within(1e-9);
        await Assert.That(position.Y).IsEqualTo(15.0).Within(1e-9);
        await Assert.That(direction.X).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(direction.Y).IsEqualTo(-5.0).Within(1e-9);
    }

    [Test]
    public async Task Reverse_MirrorsTheVerticalCurve()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.Reverse();

        var curve = Profile(document.ToJson())[1]!;
        await Assert.That(Type(curve)).IsEqualTo(VerticalType);
        var start = At(curve, "autodesk.math:point2d-1.0.0", "startPoint");
        var pvi = At(curve, "autodesk.math:point2d-1.0.0", "pvi");
        var end = At(curve, "autodesk.math:point2d-1.0.0", "endPoint");
        await Assert.That(start.X).IsEqualTo(Length - 180).Within(1e-9);
        await Assert.That(start.Y).IsEqualTo(16.0).Within(1e-9);
        await Assert.That(pvi.X).IsEqualTo(Length - 140).Within(1e-9);
        await Assert.That(pvi.Y).IsEqualTo(17.0).Within(1e-9);
        await Assert.That(end.X).IsEqualTo(Length - 100).Within(1e-9);
        await Assert.That(end.Y).IsEqualTo(15.0).Within(1e-9);
    }

    [Test]
    public async Task Reverse_ProfileArc_IsMirroredAndKeepsItsHandedness()
    {
        // a crest drawn as a counter-clockwise arc (a sag) of radius 10 centred at (50, 20), swept a quarter turn from the west
        var profile = Line(0, 10, 40, 10) + "," + Circle(50, 20, -10, 0, 0.25, false);
        var document = AlignmentJsonDocument.Parse(Document(Line(0, 0, Length, 0), profile));

        document.Reverse();

        var circle = Profile(document.ToJson())[0]!;
        var center = At(circle, "autodesk.math:point2d-1.0.0", "center");
        var radius = At(circle, "autodesk.math:vector2d-1.0.0", "radius");
        await Assert.That(center.X).IsEqualTo(Length - 50).Within(1e-9);
        await Assert.That(center.Y).IsEqualTo(20.0).Within(1e-9);
        // the old end of the arc is directly below the centre at (50, 10); mirrored it stays directly below
        await Assert.That(radius.X).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(radius.Y).IsEqualTo(-10.0).Within(1e-9);
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
    }

    // ---- reversing: general -------------------------------------------------------------------------------------

    [Test]
    public async Task Reverse_Twice_GivesBackTheSameGeometry()
    {
        var original = Sample("Twice", 5.0);
        var document = AlignmentJsonDocument.Parse(original);

        document.Reverse();
        var middle = AlignmentJsonDocument.Parse(document.ToJson());
        middle.Reverse();

        var before = JsonNode.Parse(original)!;
        var after = JsonNode.Parse(middle.ToJson())!;
        await Assert.That(CompareNumbers(before, after)).IsLessThan(1e-9);
    }

    [Test]
    public async Task Reverse_WritesEveryFloatWithADecimalPoint()
    {
        var document = AlignmentJsonDocument.Parse(Sample());

        document.Reverse();

        await Assert.That(IntegerLookingFloats(document.ToJson()).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Reverse_AlignmentWithNoCurves_IsHarmless()
    {
        var document = AlignmentJsonDocument.Parse("{\"autodesk.infra:alignment-1.0.0\":{\"CoreComponent\":{}}}");

        document.Reverse();

        await Assert.That(document.PlanLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task CanReverse_UnknownPlanCurve_IsRefusedWithTheCurveType()
    {
        var weird = Sample().Replace(CircleType, "autodesk.infra.geometry:spiral2d-1.0.0");
        var document = AlignmentJsonDocument.Parse(weird);

        var canReverse = document.CanReverse(out var reason);

        await Assert.That(canReverse).IsFalse();
        await Assert.That(reason.Contains("spiral2d")).IsTrue();
        await Assert.That(reason.Contains("plan")).IsTrue();
        await Assert.That(Catch(() => document.Reverse()) is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task CanReverse_UnknownProfileCurve_IsRefused()
    {
        var weird = Sample().Replace(VerticalType, "autodesk.infra.geometry:polyline2d-1.0.0");
        var document = AlignmentJsonDocument.Parse(weird);

        var canReverse = document.CanReverse(out var reason);

        await Assert.That(canReverse).IsFalse();
        await Assert.That(reason.Contains("profile")).IsTrue();
    }

    [Test]
    public async Task CanReverse_Supported_HasNoReason()
    {
        var canReverse = AlignmentJsonDocument.Parse(Sample()).CanReverse(out var reason);

        await Assert.That(canReverse).IsTrue();
        await Assert.That(reason).IsNull();
    }

    [Test]
    public async Task Edits_CanBeCombined()
    {
        var document = AlignmentJsonDocument.Parse(Sample("A", 1.0));

        document.Name = "B";
        document.StartStation = 2.0;
        document.Reverse();

        var again = AlignmentJsonDocument.Parse(document.ToJson());
        await Assert.That(again.Name).IsEqualTo("B");
        await Assert.That(again.StartStation).IsEqualTo(2.0);
        await Assert.That(Type(Horizontal(document.ToJson())[1]!)).IsEqualTo(CircleType);
    }

    /// <summary>Largest difference between two JSON documents of the same shape, comparing numbers.</summary>
    private static double CompareNumbers(JsonNode a, JsonNode b)
    {
        switch (a)
        {
            case JsonObject objectA:
                var worstObject = 0.0;
                foreach (var pair in objectA)
                {
                    worstObject = Math.Max(worstObject, CompareNumbers(pair.Value, ((JsonObject)b)[pair.Key]));
                }

                return worstObject;

            case JsonArray arrayA:
                var worstArray = 0.0;
                var arrayB = (JsonArray)b;
                if (arrayA.Count != arrayB.Count)
                {
                    return double.MaxValue;
                }

                for (var i = 0; i < arrayA.Count; i++)
                {
                    worstArray = Math.Max(worstArray, CompareNumbers(arrayA[i], arrayB[i]));
                }

                return worstArray;

            case null:
                return b is null ? 0.0 : double.MaxValue;

            default:
                var element = a.GetValue<object>();
                if (element is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    return Math.Abs(json.GetDouble() - b.GetValue<double>());
                }

                return a.ToJsonString() == b.ToJsonString() ? 0.0 : double.MaxValue;
        }
    }
}
