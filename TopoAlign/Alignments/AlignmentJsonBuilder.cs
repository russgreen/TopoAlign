using Autodesk.Revit.DB;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TopoAlign.Alignments;

/// <summary>
/// Builds the Autodesk "infra" alignment JSON that Revit's Civil Alignments add-in stores on an alignment.
/// The schema is a set of typed groups: primitives are nested under "String" / "Float64" / "Bool" / "enum&lt;Enum&gt;"
/// and objects under a versioned type key. Curve primitives are 2D lines (position + direction vector) and
/// circles (centre + start radius vector + sweep as a fraction of a full turn + clockwise flag).
/// Coordinates are written in feet (Revit internal units) with CoordSysUnitScaleToMeter = 0.3048, in shared
/// (survey) coordinates. Horizontal geometry is X/Y; profile geometry is distance along the alignment (x) /
/// elevation (y). The user's start station is the CoreComponent "startstation"; Autodesk's displayed start and
/// end distances are distances along the alignment (0 .. length), not stations.
/// All Float64 values must be written with a decimal point.
/// </summary>
internal static class AlignmentJsonBuilder
{
    private const double FeetToMeters = 0.3048;
    private const double ShortCurveTolerance = 1e-6;

    private const string LineTypeId = "autodesk.infra.geometry:line2d-1.0.0";
    private const string CircleTypeId = "autodesk.infra.geometry:circle2d-1.0.0";

    private const string AecAlignmentDataKey = "autodesk.infra:aecalignmentdata-1.0.0";
    private const string AlignmentKey = "autodesk.infra:alignment-1.0.0";
    private const string CurveGeometryKey = "autodesk.infra.geometry:curvegeometry-1.0.0";
    private const string HorizontalGeometryKey = "autodesk.infra:horizontalcurvegeometry2d-1.0.0";
    private const string ProfileGeometryKey = "autodesk.infra:profilecurvegeometry2d-1.0.0";
    private const string CurveArrayKey = "array<autodesk.infra.geometry:curve2d-1.0.0>";
    private const string ParamRangeKey = "autodesk.geometry:paramrange-1.0.0";
    private const string Point2DKey = "autodesk.math:point2d-1.0.0";
    private const string Vector2DKey = "autodesk.math:vector2d-1.0.0";

    /// <summary>
    /// Builds the alignment JSON for an ordered, continuous chain of lines and arcs.
    /// The profile is a single flat line at the elevation of the first curve.
    /// </summary>
    /// <param name="name">Alignment name.</param>
    /// <param name="orderedCurves">Continuous chain of <see cref="Line"/> / <see cref="Arc"/> in internal units.</param>
    /// <param name="internalToShared">Transform from internal to shared coordinates (inverse of the project location's total transform).</param>
    /// <param name="startStation">Station at the start of the alignment, in feet (internal units).</param>
    /// <param name="planLength">Plan length of the alignment in feet, as written to the JSON.</param>
    public static string Build(string name, IReadOnlyList<Curve> orderedCurves, Transform internalToShared, double startStation, out double planLength)
    {
        if (orderedCurves is null || orderedCurves.Count == 0)
        {
            throw new ArgumentException("At least one chained curve is required.", nameof(orderedCurves));
        }

        internalToShared ??= Transform.Identity;

        var horizontalCurves = new JsonArray();
        var length = 0.0;

        foreach (var curve in orderedCurves)
        {
            var node = curve switch
            {
                Line line => CreateLine(line, internalToShared),
                Arc arc => CreateCircle(arc, internalToShared),
                _ => throw new NotSupportedException($"Unsupported curve type '{curve.GetType().Name}'.")
            };

            if (node is null)
            {
                continue;
            }

            horizontalCurves.Add(node);
            length += Length2D(curve, internalToShared);
        }

        if (horizontalCurves.Count == 0)
        {
            throw new InvalidOperationException("The selected curves have no usable length.");
        }

        planLength = length;

        var elevation = internalToShared.OfPoint(orderedCurves[0].GetEndPoint(0)).Z;
        var profileCurves = new JsonArray
        {
            CreateLineNode(0.0, elevation, length, 0.0)
        };

        var root = new JsonObject
        {
            ["String"] = new JsonObject { ["guid"] = Guid.NewGuid().ToString() },
            [AecAlignmentDataKey] = new JsonObject
            {
                ["Component"] = new JsonObject
                {
                    ["Float64"] = new JsonObject { ["CoordSysUnitScaleToMeter"] = Number(FeetToMeters) },
                    ["String"] = new JsonObject { ["CoordSysWKT"] = string.Empty }
                }
            },
            [AlignmentKey] = new JsonObject
            {
                ["CoreComponent"] = new JsonObject
                {
                    ["Bool"] = new JsonObject { ["chordBasedStations"] = false },
                    ["Float64"] = new JsonObject
                    {
                        ["startstation"] = Number(startStation),
                        ["unitChordLen"] = Number(100.0)
                    },
                    ["String"] = new JsonObject
                    {
                        ["createdOn"] = string.Empty,
                        ["description"] = string.Empty,
                        ["guid"] = Guid.NewGuid().ToString(),
                        ["lastEditedOn"] = string.Empty,
                        ["name"] = name ?? string.Empty,
                        ["source"] = "TopoAlign"
                    },
                    [CurveGeometryKey] = new JsonObject
                    {
                        ["curveGeometry"] = new JsonObject
                        {
                            [HorizontalGeometryKey] = new JsonObject
                            {
                                ["horizontalGeometry"] = CreateGeometry(horizontalCurves)
                            },
                            [ProfileGeometryKey] = new JsonObject
                            {
                                ["profileGeometry"] = CreateGeometry(profileCurves)
                            }
                        }
                    }
                }
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>Plan length (feet) of a line/arc after dropping Z; used for the profile extent.</summary>
    private static double Length2D(Curve curve, Transform internalToShared)
    {
        if (curve is Arc arc)
        {
            var (_, radius, sweep, _) = GetArcParameters(arc, internalToShared);
            return radius * sweep;
        }

        var start = internalToShared.OfPoint(curve.GetEndPoint(0));
        var end = internalToShared.OfPoint(curve.GetEndPoint(1));
        return Distance2D(start, end);
    }

    private static JsonObject CreateGeometry(JsonArray curves)
    {
        return new JsonObject
        {
            ["String"] = new JsonObject { ["tag"] = string.Empty },
            [CurveArrayKey] = new JsonObject { ["curves"] = curves },
            [ParamRangeKey] = new JsonObject { ["range"] = CreateRange(0.0, 0.0, 0) },
            ["Spirals"] = new JsonObject { ["Spirals"] = new JsonArray() }
        };
    }

    private static JsonObject CreateLine(Line line, Transform internalToShared)
    {
        var start = internalToShared.OfPoint(line.GetEndPoint(0));
        var end = internalToShared.OfPoint(line.GetEndPoint(1));

        if (Distance2D(start, end) < ShortCurveTolerance)
        {
            return null;
        }

        return CreateLineNode(start.X, start.Y, end.X - start.X, end.Y - start.Y);
    }

    private static JsonObject CreateLineNode(double x, double y, double dx, double dy)
    {
        return new JsonObject
        {
            [Point2DKey] = new JsonObject { ["position"] = CreatePoint(x, y) },
            [Vector2DKey] = new JsonObject { ["direction"] = CreatePoint(dx, dy) },
            ["String"] = new JsonObject { ["guid"] = Guid.NewGuid().ToString() },
            [ParamRangeKey] = new JsonObject { ["range"] = CreateRange(0.0, 0.0, 0) },
            ["typeid"] = LineTypeId
        };
    }

    private static JsonObject CreateCircle(Arc arc, Transform internalToShared)
    {
        var (center, radius, sweep, clockwise) = GetArcParameters(arc, internalToShared);

        if (radius * sweep < ShortCurveTolerance)
        {
            return null;
        }

        var start = internalToShared.OfPoint(arc.GetEndPoint(0));

        return new JsonObject
        {
            ["Bool"] = new JsonObject { ["clockwise"] = clockwise },
            [Point2DKey] = new JsonObject { ["center"] = CreatePoint(center.X, center.Y) },
            [Vector2DKey] = new JsonObject { ["radius"] = CreatePoint(start.X - center.X, start.Y - center.Y) },
            ["String"] = new JsonObject { ["guid"] = Guid.NewGuid().ToString() },
            [ParamRangeKey] = new JsonObject { ["range"] = CreateRange(0.0, sweep / (2.0 * Math.PI), 1) },
            ["typeid"] = CircleTypeId
        };
    }

    /// <summary>
    /// Plan-view centre, radius, sweep angle (radians, 0..2π) and direction of an arc in shared coordinates.
    /// Direction comes from start→mid→end rather than the arc normal, which depends on how the arc was drawn.
    /// </summary>
    private static (XYZ Center, double Radius, double Sweep, bool Clockwise) GetArcParameters(Arc arc, Transform internalToShared)
    {
        var center = internalToShared.OfPoint(arc.Center);
        var start = internalToShared.OfPoint(arc.GetEndPoint(0));
        var end = internalToShared.OfPoint(arc.GetEndPoint(1));
        var mid = internalToShared.OfPoint(arc.Evaluate(0.5, true));

        var radius = Distance2D(center, start);

        var cross = (mid.X - start.X) * (end.Y - mid.Y) - (mid.Y - start.Y) * (end.X - mid.X);
        var clockwise = cross < 0.0;

        var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
        var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);

        var sweep = clockwise ? startAngle - endAngle : endAngle - startAngle;
        while (sweep <= 0.0)
        {
            sweep += 2.0 * Math.PI;
        }

        while (sweep > 2.0 * Math.PI)
        {
            sweep -= 2.0 * Math.PI;
        }

        return (center, radius, sweep, clockwise);
    }

    private static JsonObject CreatePoint(double x, double y)
    {
        return new JsonObject
        {
            ["Float64"] = new JsonObject { ["x"] = Number(x), ["y"] = Number(y) }
        };
    }

    private static JsonObject CreateRange(double low, double high, int type)
    {
        return new JsonObject
        {
            ["Float64"] = new JsonObject { ["high"] = Number(high), ["low"] = Number(low) },
            ["enum<Enum>"] = new JsonObject { ["type"] = type }
        };
    }

    /// <summary>
    /// A JSON number that is always written with a decimal point or exponent ("100.0", never "100").
    /// Autodesk's parser rejects integer tokens for Float64 values, and System.Text.Json writes a
    /// whole-number double as an integer unless the token is kept verbatim.
    /// </summary>
    private static JsonNode Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Alignment geometry contains a non-finite value.");
        }

        var text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (text.IndexOfAny(new[] { '.', 'E', 'e' }) < 0)
        {
            text += ".0";
        }

        return JsonNode.Parse(text);
    }

    private static double Distance2D(XYZ a, XYZ b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
