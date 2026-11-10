using Autodesk.Revit.DB;
using System.Text.Json;
using System.Text.Json.Nodes;
using TopoAlign.Alignments.Plan;
using TopoAlign.Alignments.Profile;

namespace TopoAlign.Alignments.AlignmentJson;

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

    private const string LineTypeId = InfraJson.LineTypeId;
    private const string CircleTypeId = InfraJson.CircleTypeId;

    private const string AecAlignmentDataKey = InfraJson.AecAlignmentDataKey;
    private const string AlignmentKey = InfraJson.AlignmentKey;
    private const string CurveGeometryKey = InfraJson.CurveGeometryKey;
    private const string HorizontalGeometryKey = InfraJson.HorizontalGeometryKey;
    private const string ProfileGeometryKey = InfraJson.ProfileGeometryKey;
    private const string CurveArrayKey = InfraJson.CurveArrayKey;
    private const string ParamRangeKey = InfraJson.ParamRangeKey;
    private const string Point2DKey = InfraJson.Point2DKey;
    private const string Vector2DKey = InfraJson.Vector2DKey;

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
        return Build(name, orderedCurves, internalToShared, startStation, null, 0.0, out planLength);
    }

    /// <summary>
    /// Builds the alignment JSON with an explicit profile.
    /// </summary>
    /// <param name="profilePvis">
    /// Profile points (distance along the alignment, elevation in internal feet) starting at 0 and ending at the plan
    /// length; or null for a flat profile at the elevation of the first curve.
    /// </param>
    /// <param name="minVerticalCurveLength">Minimum parabolic vertical curve length in feet at each PVI; 0 for a polyline profile.</param>
    public static string Build(string name, IReadOnlyList<Curve> orderedCurves, Transform internalToShared, double startStation, IReadOnlyList<ProfilePoint>? profilePvis, double minVerticalCurveLength, out double planLength)
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

        var profileCurves = CreateProfile(profilePvis, minVerticalCurveLength, internalToShared, length,
            () => orderedCurves[0].GetEndPoint(0).Z);

        return AssembleDocument(name, horizontalCurves, profileCurves, startStation);
    }

    /// <summary>
    /// Builds the alignment JSON for a chain of plan segments (lines, arcs and spirals) laid out in internal coordinates.
    /// Autodesk's parser doesn't accept native spirals, so spirals are written as short straights within
    /// <paramref name="spiralDeviation"/> feet of the true curve, the same way other alignment tools do.
    /// </summary>
    /// <param name="flatElevation">Internal elevation of the flat profile used when <paramref name="profilePvis"/> is null.</param>
    public static string BuildFromSegments(string name, IReadOnlyList<PlanSegment> segments, Transform internalToShared, double startStation, double flatElevation,
        IReadOnlyList<ProfilePoint>? profilePvis, double minVerticalCurveLength, double spiralDeviation, out double planLength)
    {
        if (segments is null || segments.Count == 0)
        {
            throw new ArgumentException("At least one segment is required.", nameof(segments));
        }

        internalToShared ??= Transform.Identity;

        var horizontalCurves = new JsonArray();
        var length = 0.0;

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case PlanLine line:
                    length += AddSharedLine(horizontalCurves, line.StartPoint, line.EndPoint, internalToShared);
                    break;

                case PlanArc arc:
                    length += AddSharedArc(horizontalCurves, arc, internalToShared);
                    break;

                case PlanSpiral spiral:
                    var points = spiral.Tessellate(spiralDeviation);
                    for (var i = 1; i < points.Count; i++)
                    {
                        length += AddSharedLine(horizontalCurves, points[i - 1], points[i], internalToShared);
                    }

                    break;

                default:
                    throw new NotSupportedException($"Unsupported segment type '{segment.GetType().Name}'.");
            }
        }

        if (horizontalCurves.Count == 0)
        {
            throw new InvalidOperationException("The alignment has no usable length.");
        }

        planLength = length;

        var profileCurves = CreateProfile(profilePvis, minVerticalCurveLength, internalToShared, length, () => flatElevation);
        return AssembleDocument(name, horizontalCurves, profileCurves, startStation);
    }

    private static double AddSharedLine(JsonArray target, PlanPoint start, PlanPoint end, Transform internalToShared)
    {
        var from = internalToShared.OfPoint(new XYZ(start.X, start.Y, 0.0));
        var to = internalToShared.OfPoint(new XYZ(end.X, end.Y, 0.0));
        var length = Distance2D(from, to);
        if (length < ShortCurveTolerance)
        {
            return 0.0;
        }

        target.Add(CreateLineNode(from.X, from.Y, to.X - from.X, to.Y - from.Y));
        return length;
    }

    private static double AddSharedArc(JsonArray target, PlanArc arc, Transform internalToShared)
    {
        if (arc.Length < ShortCurveTolerance)
        {
            return 0.0;
        }

        var center = internalToShared.OfPoint(new XYZ(arc.Center.X, arc.Center.Y, 0.0));
        var start = internalToShared.OfPoint(new XYZ(arc.StartPoint.X, arc.StartPoint.Y, 0.0));
        target.Add(CreateCircleNode(center.X, center.Y, start.X - center.X, start.Y - center.Y,
            Math.Abs(arc.Sweep) / (2.0 * Math.PI), arc.IsClockwise));
        return arc.Length;
    }

    private static JsonArray CreateProfile(IReadOnlyList<ProfilePoint>? profilePvis, double minVerticalCurveLength, Transform internalToShared, double length, Func<double> flatElevation)
    {
        if (profilePvis is null)
        {
            return new JsonArray
            {
                CreateLineNode(0.0, SharedElevation(flatElevation(), internalToShared), length, 0.0)
            };
        }

        return CreateProfileCurves(profilePvis, minVerticalCurveLength, internalToShared, length);
    }

    private static string AssembleDocument(string name, JsonArray horizontalCurves, JsonArray profileCurves, double startStation)
    {
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

    /// <summary>Plan length (feet) of a line/arc after dropping Z. This is the alignment's stationing length.</summary>
    internal static double PlanLength(Curve curve)
    {
        return Length2D(curve, Transform.Identity);
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

    /// <summary>
    /// Profile curves (lines and parabolic vertical curves) for the PVIs. Stations are distances along the alignment;
    /// elevations are converted to shared coordinates (the project location's vertical offset).
    /// </summary>
    private static JsonArray CreateProfileCurves(IReadOnlyList<ProfilePoint> pvis, double minVerticalCurveLength, Transform internalToShared, double planLength)
    {
        if (pvis.Count < 2)
        {
            throw new ArgumentException("A profile needs at least two points.", nameof(pvis));
        }

        const double endTolerance = 1e-3;
        if (Math.Abs(pvis[0].Station) > endTolerance || Math.Abs(pvis[pvis.Count - 1].Station - planLength) > endTolerance)
        {
            throw new ArgumentException("The profile must run from the start to the end of the alignment.", nameof(pvis));
        }

        // lock the ends to exactly 0 and the written plan length so the profile always covers the alignment
        var points = new List<ProfilePoint>(pvis.Count);
        for (var i = 0; i < pvis.Count; i++)
        {
            var station = i == 0 ? 0.0 : i == pvis.Count - 1 ? planLength : pvis[i].Station;
            points.Add(new ProfilePoint(station, SharedElevation(pvis[i].Elevation, internalToShared)));
        }

        return ProfileJson.CreateCurves(points, minVerticalCurveLength);
    }

    private static double SharedElevation(double internalElevation, Transform internalToShared)
    {
        return internalToShared.OfPoint(new XYZ(0.0, 0.0, internalElevation)).Z;
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

    private static JsonObject? CreateLine(Line line, Transform internalToShared)
    {
        var start = internalToShared.OfPoint(line.GetEndPoint(0));
        var end = internalToShared.OfPoint(line.GetEndPoint(1));

        if (Distance2D(start, end) < ShortCurveTolerance)
        {
            return null;
        }

        return CreateLineNode(start.X, start.Y, end.X - start.X, end.Y - start.Y);
    }

    private static JsonObject CreateLineNode(double x, double y, double dx, double dy) => ProfileJson.CreateLineNode(x, y, dx, dy);

    private static JsonObject? CreateCircle(Arc arc, Transform internalToShared)
    {
        var (center, radius, sweep, clockwise) = GetArcParameters(arc, internalToShared);

        if (radius * sweep < ShortCurveTolerance)
        {
            return null;
        }

        var start = internalToShared.OfPoint(arc.GetEndPoint(0));
        return CreateCircleNode(center.X, center.Y, start.X - center.X, start.Y - center.Y, sweep / (2.0 * Math.PI), clockwise);
    }

    /// <summary>A circle2d curve: centre, the radius vector to the start point, the sweep as a fraction of a full turn, and the direction.</summary>
    private static JsonObject CreateCircleNode(double centerX, double centerY, double startVectorX, double startVectorY, double turns, bool clockwise)
    {
        return ProfileJson.CreateCircleNode(centerX, centerY, startVectorX, startVectorY, turns, clockwise);
    }

    /// <summary>
    /// Plan-view centre, radius, sweep angle (radians, 0..2π) and direction of an arc in shared coordinates.
    /// Direction comes from start→mid→end rather than the arc normal, which depends on how the arc was drawn.
    /// </summary>
    internal static (XYZ Center, double Radius, double Sweep, bool Clockwise) GetArcParameters(Arc arc, Transform internalToShared)
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

    private static JsonObject CreatePoint(double x, double y) => ProfileJson.CreatePoint(x, y);

    private static JsonObject CreateRange(double low, double high, int type) => ProfileJson.CreateRange(low, high, type);

    private static JsonNode Number(double value) => InfraJson.Number(value);

    private static double Distance2D(XYZ a, XYZ b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
