using Autodesk.Revit.DB;
using System.Globalization;
using System.Text.Json.Nodes;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;

namespace TopoAlign.Tests;

/// <summary>
/// Static helpers shared by the API and UI test base classes: geometry builders, solids, exception capture,
/// culture/STA helpers and alignment JSON navigation. Test files import it with using static.
/// </summary>
public static class TestKit
{
    /// <summary>Creates an alignment from two lines (100 ft east, then 50 ft north) and returns its element id.</summary>
    public static ElementId CreateAlignment(Document document, string name = "Road 1", double startStation = 0.0, ICollection<ElementId> delete = null)
    {
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var json = AlignmentJsonBuilder.Build(name, new Curve[] { Ln(0, 0, 100, 0), Ln(100, 0, 100, 50) }, internalToShared, startStation, out var planLength);
        return AlignmentApi.Create(document, name, json, planLength, delete);
    }

    /// <summary>Creates a model line in a document inside its own transaction.</summary>
    public static ModelCurve NewModelCurve(Document document, Curve curve)
    {
        using var transaction = new Transaction(document, "Create model curve");
        transaction.Start();

        var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
        var sketchPlane = SketchPlane.Create(document, plane);
        var modelCurve = document.Create.NewModelCurve(curve, sketchPlane);

        transaction.Commit();
        return modelCurve;
    }

    public static Line Ln(double x0, double y0, double x1, double y1, double z = 0.0)
    {
        return Line.CreateBound(new XYZ(x0, y0, z), new XYZ(x1, y1, z));
    }

    /// <summary>Arc through three points in the XY plane.</summary>
    public static Arc ArcThrough(double x0, double y0, double xm, double ym, double x1, double y1, double z = 0.0)
    {
        return Arc.Create(new XYZ(x0, y0, z), new XYZ(x1, y1, z), new XYZ(xm, ym, z));
    }

    /// <summary>Quarter circle of the given radius about the origin, counter-clockwise from +X to +Y.</summary>
    public static Arc QuarterArcCcw(double radius)
    {
        var d = radius * Math.Sqrt(0.5);
        return ArcThrough(radius, 0, d, d, 0, radius);
    }

    /// <summary>Quarter circle of the given radius about the origin, clockwise from +X to -Y.</summary>
    public static Arc QuarterArcCw(double radius)
    {
        var d = radius * Math.Sqrt(0.5);
        return ArcThrough(radius, 0, d, -d, 0, -radius);
    }

    /// <summary>Closed rectangle in the XY plane with its corner at the origin.</summary>
    public static CurveLoop RectangleLoop(double width, double depth, double z = 0.0)
    {
        return CurveLoop.Create(new List<Curve>
        {
            Ln(0, 0, width, 0, z),
            Ln(width, 0, width, depth, z),
            Ln(width, depth, 0, depth, z),
            Ln(0, depth, 0, 0, z)
        });
    }

    /// <summary>A box solid with its base corner at the origin.</summary>
    public static Solid Box(double width, double depth, double height)
    {
        return GeometryCreationUtilities.CreateExtrusionGeometry(
            new List<CurveLoop> { RectangleLoop(width, depth) }, XYZ.BasisZ, height);
    }

    /// <summary>A vertical cylinder solid centred on the origin.</summary>
    public static Solid Cylinder(double radius, double height)
    {
        var circle = CurveLoop.Create(new List<Curve>
        {
            Arc.Create(XYZ.Zero, radius, 0, Math.PI, XYZ.BasisX, XYZ.BasisY),
            Arc.Create(XYZ.Zero, radius, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY)
        });
        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { circle }, XYZ.BasisZ, height);
    }

    public static IEnumerable<PlanarFace> PlanarFaces(Solid solid)
    {
        return solid.Faces.Cast<Face>().OfType<PlanarFace>();
    }

    public static Curve Reversed(Curve curve)
    {
        return curve switch
        {
            Line => Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0)),
            Arc => Arc.Create(curve.GetEndPoint(1), curve.GetEndPoint(0), curve.Evaluate(0.5, true)),
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>Runs an action and returns the exception it threw, or null if it did not throw.</summary>
    public static Exception Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Runs an action with the invariant culture so number formatting is predictable.</summary>
    public static void WithInvariantCulture(Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Runs an action on a dedicated STA thread (needed for WPF controls) and rethrows any failure.</summary>
    public static void RunOnStaThread(Action action)
    {
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("STA thread failed: " + failure.Message, failure);
        }
    }

    // ---- alignment JSON navigation -------------------------------------------------------------------------------

    public const string AlignmentKey = "autodesk.infra:alignment-1.0.0";
    public const string AecAlignmentDataKey = "autodesk.infra:aecalignmentdata-1.0.0";
    public const string CurveGeometryKey = "autodesk.infra.geometry:curvegeometry-1.0.0";
    public const string HorizontalKey = "autodesk.infra:horizontalcurvegeometry2d-1.0.0";
    public const string ProfileKey = "autodesk.infra:profilecurvegeometry2d-1.0.0";
    public const string CurvesKey = "array<autodesk.infra.geometry:curve2d-1.0.0>";
    public const string Point2DKey = "autodesk.math:point2d-1.0.0";
    public const string Vector2DKey = "autodesk.math:vector2d-1.0.0";
    public const string RangeKey = "autodesk.geometry:paramrange-1.0.0";
    public const string LineTypeId = "autodesk.infra.geometry:line2d-1.0.0";
    public const string CircleTypeId = "autodesk.infra.geometry:circle2d-1.0.0";

    public static JsonObject Parse(string json)
    {
        return (JsonObject)JsonNode.Parse(json)!;
    }

    public static JsonObject CoreComponent(JsonObject root)
    {
        return (JsonObject)root[AlignmentKey]!["CoreComponent"]!;
    }

    public static JsonObject GeometryBlock(JsonObject root, string geometryKey, string geometryName)
    {
        return (JsonObject)CoreComponent(root)[CurveGeometryKey]!["curveGeometry"]![geometryKey]![geometryName]!;
    }

    public static JsonObject HorizontalGeometry(JsonObject root) => GeometryBlock(root, HorizontalKey, "horizontalGeometry");

    public static JsonObject ProfileGeometry(JsonObject root) => GeometryBlock(root, ProfileKey, "profileGeometry");

    public static JsonArray Curves(JsonObject geometry)
    {
        return (JsonArray)geometry[CurvesKey]!["curves"]!;
    }

    public static (double X, double Y) PointOf(JsonNode curve, string groupKey, string name)
    {
        var values = curve[groupKey]![name]!["Float64"]!;
        return (values["x"]!.GetValue<double>(), values["y"]!.GetValue<double>());
    }

    public static string TypeId(JsonNode curve) => curve["typeid"]!.GetValue<string>();

    public static (double Low, double High, int Type) RangeOf(JsonNode curve)
    {
        var range = curve[RangeKey]!["range"]!;
        return (
            range["Float64"]!["low"]!.GetValue<double>(),
            range["Float64"]!["high"]!.GetValue<double>(),
            range["enum<Enum>"]!["type"]!.GetValue<int>());
    }
}
