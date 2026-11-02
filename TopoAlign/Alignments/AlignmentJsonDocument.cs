using System.Text.Json.Nodes;

namespace TopoAlign.Alignments;

/// <summary>
/// An existing alignment's infra JSON opened for editing: the name, the start station and the direction.
/// Pure JSON work with no Revit types. Values that aren't changed keep their original text, so a document written by
/// another tool round-trips unchanged; every number written here has a decimal point, as Autodesk's parser needs.
/// </summary>
internal sealed class AlignmentJsonDocument
{
    private readonly JsonObject _root;
    private readonly JsonObject _core;

    private AlignmentJsonDocument(JsonObject root, JsonObject core)
    {
        _root = root;
        _core = core;
    }

    /// <summary>Parses alignment JSON. Throws <see cref="InvalidOperationException"/> if it isn't an alignment document.</summary>
    public static AlignmentJsonDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("The alignment has no JSON data.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException("The alignment data could not be read: " + ex.Message, ex);
        }

        var root = node as JsonObject;
        var core = root?[InfraJson.AlignmentKey]?["CoreComponent"] as JsonObject;
        if (root is null || core is null)
        {
            throw new InvalidOperationException("The data is not an alignment document.");
        }

        return new AlignmentJsonDocument(root, core);
    }

    /// <summary>The alignment's name as stored in the JSON.</summary>
    public string Name
    {
        get => (_core["String"] as JsonObject)?["name"]?.GetValue<string>() ?? string.Empty;
        set => EnsureObject(_core, "String")["name"] = value ?? string.Empty;
    }

    /// <summary>Station at the start of the alignment in feet.</summary>
    public double StartStation
    {
        get => ReadDouble((_core["Float64"] as JsonObject)?["startstation"]) ?? 0.0;
        set => EnsureObject(_core, "Float64")["startstation"] = InfraJson.Number(value);
    }

    /// <summary>Length of the horizontal alignment in feet, from its lines and arcs.</summary>
    public double PlanLength
    {
        get
        {
            var total = 0.0;
            foreach (var curve in Curves(HorizontalGeometry()).OfType<JsonNode>())
            {
                total += CurveLength(curve);
            }

            return total;
        }
    }

    /// <summary>
    /// True if every horizontal and profile curve is a kind that <see cref="Reverse"/> understands (straights, arcs and
    /// parabolic vertical curves); otherwise the reason it can't.
    /// </summary>
    public bool CanReverse(out string? reason)
    {
        reason = null;
        foreach (var curve in Curves(HorizontalGeometry()).OfType<JsonNode>())
        {
            var typeId = TypeId(curve);
            if (typeId != InfraJson.LineTypeId && typeId != InfraJson.CircleTypeId)
            {
                reason = $"The alignment contains a '{typeId ?? "unknown"}' curve in plan, which can't be reversed.";
                return false;
            }
        }

        foreach (var curve in Curves(ProfileGeometry()).OfType<JsonNode>())
        {
            var typeId = TypeId(curve);
            if (typeId != InfraJson.LineTypeId && typeId != InfraJson.CircleTypeId && typeId != InfraJson.VerticalCurveTypeId)
            {
                reason = $"The alignment contains a '{typeId ?? "unknown"}' curve in its profile, which can't be reversed.";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reverses the direction of the alignment: the plan curves run the other way and the profile is mirrored about the
    /// middle of the alignment so each point keeps its elevation. Throws <see cref="InvalidOperationException"/> if
    /// <see cref="CanReverse"/> is false.
    /// </summary>
    public void Reverse()
    {
        if (!CanReverse(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        var length = PlanLength;

        var horizontal = Curves(HorizontalGeometry());
        foreach (var curve in horizontal.OfType<JsonNode>())
        {
            if (TypeId(curve) == InfraJson.LineTypeId)
            {
                ReverseLine(curve);
            }
            else
            {
                ReverseCircle(curve, mirrorAbout: null);
            }
        }

        ReverseOrder(horizontal);

        var profile = Curves(ProfileGeometry());
        foreach (var curve in profile.OfType<JsonNode>())
        {
            switch (TypeId(curve))
            {
                case InfraJson.LineTypeId:
                    MirrorProfileLine(curve, length);
                    break;

                case InfraJson.VerticalCurveTypeId:
                    MirrorVerticalCurve(curve, length);
                    break;

                default:
                    ReverseCircle(curve, mirrorAbout: length);
                    break;
            }
        }

        ReverseOrder(profile);
    }

    // ---- profile editing ----------------------------------------------------------------------------------------

    /// <summary>
    /// True if the profile is made only of straights and parabolic vertical curves, the kinds <see cref="ReadProfile"/> and
    /// <see cref="SetProfile"/> understand; otherwise the reason it can't be edited.
    /// </summary>
    public bool CanEditProfile(out string? reason)
    {
        reason = null;
        var curves = Curves(ProfileGeometry()).OfType<JsonNode>().ToList();
        if (curves.Count == 0)
        {
            reason = "The alignment has no vertical profile.";
            return false;
        }

        foreach (var curve in curves)
        {
            var typeId = TypeId(curve);
            if (typeId != InfraJson.LineTypeId && typeId != InfraJson.VerticalCurveTypeId)
            {
                reason = $"The profile contains a '{typeId ?? "unknown"}' curve, which can't be edited here.";
                return false;
            }
        }

        return true;
    }

    /// <summary>Distance along the alignment, in feet, where the profile starts (normally 0).</summary>
    private double ProfileOrigin()
    {
        var first = Curves(ProfileGeometry()).OfType<JsonNode>().FirstOrDefault();
        if (first is null)
        {
            return 0.0;
        }

        return TypeId(first) == InfraJson.VerticalCurveTypeId
            ? ReadPoint(first, InfraJson.Point2DKey, "startPoint").X
            : ReadPoint(first, InfraJson.Point2DKey, "position").X;
    }

    /// <summary>
    /// The profile as ordered points of vertical intersection: the start, each parabolic curve's PVI (with the curve's length),
    /// each sharp change of grade and the end. Distances are measured from the start of the profile and elevations are the
    /// JSON's own (shared) elevations in feet. Throws <see cref="InvalidOperationException"/> if <see cref="CanEditProfile"/> is false.
    /// </summary>
    public List<ProfileVertex> ReadProfile()
    {
        if (!CanEditProfile(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        var curves = Curves(ProfileGeometry()).OfType<JsonNode>().ToList();
        var origin = ProfileOrigin();
        var vertices = new List<ProfileVertex>();

        void Add(double x, double y, double curveLength)
        {
            var distance = x - origin;
            if (vertices.Count > 0 && distance - vertices[vertices.Count - 1].Distance <= 1e-9)
            {
                return;
            }

            vertices.Add(new ProfileVertex(distance, y, curveLength));
        }

        for (var i = 0; i < curves.Count; i++)
        {
            var curve = curves[i];
            if (TypeId(curve) == InfraJson.VerticalCurveTypeId)
            {
                var start = ReadPoint(curve, InfraJson.Point2DKey, "startPoint");
                var pvi = ReadPoint(curve, InfraJson.Point2DKey, "pvi");
                var end = ReadPoint(curve, InfraJson.Point2DKey, "endPoint");

                if (i == 0)
                {
                    Add(start.X, start.Y, 0.0);
                }

                Add(pvi.X, pvi.Y, Math.Max(end.X - start.X, 0.0));

                if (i == curves.Count - 1)
                {
                    Add(end.X, end.Y, 0.0);
                }
            }
            else
            {
                var position = ReadPoint(curve, InfraJson.Point2DKey, "position");
                var direction = ReadPoint(curve, InfraJson.Vector2DKey, "direction");

                if (i == 0)
                {
                    Add(position.X, position.Y, 0.0);
                }

                // a line that runs into a vertical curve ends at the curve's tangent point, which isn't a PVI
                var nextIsCurve = i + 1 < curves.Count && TypeId(curves[i + 1]) == InfraJson.VerticalCurveTypeId;
                if (!nextIsCurve)
                {
                    Add(position.X + direction.X, position.Y + direction.Y, 0.0);
                }
            }
        }

        if (vertices.Count < 2)
        {
            throw new InvalidOperationException("The alignment's profile is too short to edit.");
        }

        return vertices;
    }

    /// <summary>Length of the profile in feet (the distance of its last point).</summary>
    public double ProfileLength => ReadProfile().Last().Distance;

    /// <summary>
    /// Replaces the profile with the given points and vertical curves. The first point must be at distance 0 and the last at the
    /// current end of the profile, so the profile still covers the alignment. Throws <see cref="InvalidOperationException"/> if
    /// the profile can't be edited or the points are invalid.
    /// </summary>
    public void SetProfile(IReadOnlyList<ProfileVertex> vertices)
    {
        var current = ReadProfile();
        var errors = ProfileEditing.Validate(vertices, current[current.Count - 1].Distance);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(errors[0]);
        }

        var origin = ProfileOrigin();
        var points = vertices.Select(v => new ProfilePoint(v.Distance + origin, v.Elevation)).ToList();
        var lengths = vertices.Select(v => v.CurveLength).ToList();

        var nodes = ProfileJson.CreateCurves(points, lengths);
        var holder = ProfileGeometry()?[InfraJson.CurveArrayKey] as JsonObject
                     ?? throw new InvalidOperationException("The alignment has no vertical profile.");
        holder["curves"] = nodes;
    }

    public string ToJson()
    {
        return _root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
    }

    // ---- plan curves --------------------------------------------------------------------------------------------

    /// <summary>A straight read the other way starts at its old end and points back.</summary>
    private static void ReverseLine(JsonNode curve)
    {
        var position = ReadPoint(curve, InfraJson.Point2DKey, "position");
        var direction = ReadPoint(curve, InfraJson.Vector2DKey, "direction");

        WritePoint(curve, InfraJson.Point2DKey, "position", position.X + direction.X, position.Y + direction.Y);
        WritePoint(curve, InfraJson.Vector2DKey, "direction", -direction.X, -direction.Y);
    }

    /// <summary>
    /// A circular arc read the other way starts at its old end, sweeps the other way and keeps its length. In a profile the
    /// arc is also mirrored about <paramref name="mirrorAbout"/> (a station), which flips its handedness back.
    /// </summary>
    private static void ReverseCircle(JsonNode curve, double? mirrorAbout)
    {
        var center = ReadPoint(curve, InfraJson.Point2DKey, "center");
        var radius = ReadPoint(curve, InfraJson.Vector2DKey, "radius");
        var turns = ReadDouble(curve[InfraJson.ParamRangeKey]?["range"]?["Float64"]?["high"]) ?? 0.0;
        var clockwise = curve["Bool"]?["clockwise"]?.GetValue<bool>() ?? false;

        var angle = (clockwise ? -1.0 : 1.0) * 2.0 * Math.PI * turns;
        var endX = radius.X * Math.Cos(angle) - radius.Y * Math.Sin(angle);
        var endY = radius.X * Math.Sin(angle) + radius.Y * Math.Cos(angle);

        if (mirrorAbout is null)
        {
            WritePoint(curve, InfraJson.Vector2DKey, "radius", endX, endY);
            EnsureObject((JsonObject)curve, "Bool")["clockwise"] = !clockwise;
        }
        else
        {
            // mirroring about a vertical line negates x and flips the handedness; reversing flips it back
            WritePoint(curve, InfraJson.Point2DKey, "center", mirrorAbout.Value - center.X, center.Y);
            WritePoint(curve, InfraJson.Vector2DKey, "radius", -endX, endY);
        }
    }

    // ---- profile curves -----------------------------------------------------------------------------------------

    /// <summary>A profile straight from (x, y) by (dx, dy) becomes the straight from the mirrored end back to the mirrored start.</summary>
    private static void MirrorProfileLine(JsonNode curve, double length)
    {
        var position = ReadPoint(curve, InfraJson.Point2DKey, "position");
        var direction = ReadPoint(curve, InfraJson.Vector2DKey, "direction");

        WritePoint(curve, InfraJson.Point2DKey, "position", length - (position.X + direction.X), position.Y + direction.Y);
        WritePoint(curve, InfraJson.Vector2DKey, "direction", direction.X, -direction.Y);
    }

    private static void MirrorVerticalCurve(JsonNode curve, double length)
    {
        var start = ReadPoint(curve, InfraJson.Point2DKey, "startPoint");
        var pvi = ReadPoint(curve, InfraJson.Point2DKey, "pvi");
        var end = ReadPoint(curve, InfraJson.Point2DKey, "endPoint");

        WritePoint(curve, InfraJson.Point2DKey, "startPoint", length - end.X, end.Y);
        WritePoint(curve, InfraJson.Point2DKey, "pvi", length - pvi.X, pvi.Y);
        WritePoint(curve, InfraJson.Point2DKey, "endPoint", length - start.X, start.Y);
    }

    // ---- navigation ---------------------------------------------------------------------------------------------

    private JsonObject? CurveGeometry() => _core[InfraJson.CurveGeometryKey]?["curveGeometry"] as JsonObject;

    private JsonObject? HorizontalGeometry() => CurveGeometry()?[InfraJson.HorizontalGeometryKey]?["horizontalGeometry"] as JsonObject;

    private JsonObject? ProfileGeometry() => CurveGeometry()?[InfraJson.ProfileGeometryKey]?["profileGeometry"] as JsonObject;

    private static JsonArray Curves(JsonObject? geometry)
    {
        return geometry?[InfraJson.CurveArrayKey]?["curves"] as JsonArray ?? new JsonArray();
    }

    private static string? TypeId(JsonNode? curve) => curve?["typeid"]?.GetValue<string>();

    private static double CurveLength(JsonNode curve)
    {
        if (TypeId(curve) == InfraJson.CircleTypeId)
        {
            var radius = ReadPoint(curve, InfraJson.Vector2DKey, "radius");
            var turns = ReadDouble(curve[InfraJson.ParamRangeKey]?["range"]?["Float64"]?["high"]) ?? 0.0;
            return 2.0 * Math.PI * Math.Sqrt(radius.X * radius.X + radius.Y * radius.Y) * turns;
        }

        if (TypeId(curve) == InfraJson.LineTypeId)
        {
            var direction = ReadPoint(curve, InfraJson.Vector2DKey, "direction");
            return Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        }

        return 0.0;
    }

    private static void ReverseOrder(JsonArray array)
    {
        var items = array.ToList();
        array.Clear();
        for (var i = items.Count - 1; i >= 0; i--)
        {
            array.Add(items[i]);
        }
    }

    private static (double X, double Y) ReadPoint(JsonNode curve, string groupKey, string name)
    {
        var values = curve[groupKey]?[name]?["Float64"];
        if (values is null)
        {
            throw new InvalidOperationException($"The alignment curve is missing its '{name}'.");
        }

        return (ReadDouble(values["x"]) ?? 0.0, ReadDouble(values["y"]) ?? 0.0);
    }

    private static void WritePoint(JsonNode curve, string groupKey, string name, double x, double y)
    {
        var values = curve[groupKey]?[name]?["Float64"] as JsonObject
                     ?? throw new InvalidOperationException($"The alignment curve is missing its '{name}'.");
        values["x"] = InfraJson.Number(x);
        values["y"] = InfraJson.Number(y);
    }

    private static double? ReadDouble(JsonNode? node)
    {
        return node is null ? null : node.GetValue<double>();
    }

    private static JsonObject EnsureObject(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[key] = created;
        return created;
    }
}
