using Autodesk.Revit.DB;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TopoAlign.Alignments;

internal static class AlignmentJsonBuilder
{
    private const string HorizontalGeometryKey = "horizontalGeometry";
    private const string CurvesKey = "curves";
    private const string StartStationKey = "startstation";

    public static string BuildFromTemplate(string templateJson, IReadOnlyList<Curve> orderedCurves, double startStation)
    {
        if (string.IsNullOrWhiteSpace(templateJson))
        {
            throw new ArgumentException("Template JSON is required.", nameof(templateJson));
        }

        if (orderedCurves is null || orderedCurves.Count == 0)
        {
            throw new ArgumentException("At least one chained curve is required.", nameof(orderedCurves));
        }

        var rootNode = JsonNode.Parse(templateJson) as JsonObject
            ?? throw new InvalidOperationException("Alignment JSON template is not a JSON object.");

        var horizontalNode = FindObjectRecursive(rootNode, HorizontalGeometryKey)
            ?? throw new InvalidOperationException("Unable to locate 'horizontalGeometry' in the alignment template JSON.");

        var curvesArray = FindArrayRecursive(horizontalNode, CurvesKey)
            ?? throw new InvalidOperationException("Unable to locate horizontal 'curves' in the alignment template JSON.");

        JsonObject lineTemplate = null;
        JsonObject arcTemplate = null;

        foreach (var item in curvesArray)
        {
            if (item is not JsonObject existingCurve)
            {
                continue;
            }

            if (IsArcCurveObject(existingCurve) && arcTemplate is null)
            {
                arcTemplate = existingCurve;
            }
            else if (!IsArcCurveObject(existingCurve) && lineTemplate is null)
            {
                lineTemplate = existingCurve;
            }
        }

        curvesArray.Clear();

        foreach (var curve in orderedCurves)
        {
            var sourceTemplate = curve is Arc ? arcTemplate ?? lineTemplate : lineTemplate ?? arcTemplate;
            var curveNode = sourceTemplate?.DeepClone() as JsonObject ?? CreateFallbackCurveNode(curve);

            SetCurveGeometry(curveNode, curve);
            curvesArray.Add(curveNode);
        }

        SetFirstNumericPropertyByName(rootNode, StartStationKey, startStation);

        return rootNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static JsonObject CreateFallbackCurveNode(Curve curve)
    {
        var node = new JsonObject();
        if (curve is Arc arc)
        {
            node["source"] = "autodesk.infra.geometry:curve2d-1.0.0";
            node["type"] = "arc";
            node["radius"] = arc.Radius;
        }
        else
        {
            node["source"] = "autodesk.infra.geometry:line2d-1.0.0";
            node["type"] = "line";
        }

        node["startPoint"] = new JsonArray();
        node["endPoint"] = new JsonArray();
        return node;
    }

    private static bool IsArcCurveObject(JsonObject curveObject)
    {
        var text = curveObject.ToJsonString();
        return text.IndexOf("arc", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("radius", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetCurveGeometry(JsonObject curveNode, Curve curve)
    {
        var start = curve.GetEndPoint(0);
        var end = curve.GetEndPoint(1);

        SetPointByName(curveNode, "startPoint", start.X, start.Y);
        SetPointByName(curveNode, "endPoint", end.X, end.Y);

        if (curve is Arc arc)
        {
            SetFirstNumericPropertyByName(curveNode, "radius", arc.Radius);

            var clockwise = IsClockwise(arc);
            SetFirstBooleanPropertyByName(curveNode, "clockwise", clockwise);
            SetFirstBooleanPropertyByName(curveNode, "isClockwise", clockwise);
        }
    }

    private static bool IsClockwise(Arc arc)
    {
        var start = arc.GetEndPoint(0);
        var end = arc.GetEndPoint(1);
        var center = arc.Center;

        var v1 = new XYZ(start.X - center.X, start.Y - center.Y, 0.0);
        var v2 = new XYZ(end.X - center.X, end.Y - center.Y, 0.0);
        return v1.X * v2.Y - v1.Y * v2.X < 0.0;
    }

    private static void SetPointByName(JsonNode node, string propertyName, double x, double y)
    {
        if (!TryFindPropertyNode(node, propertyName, out var pointNode, out var parentObject, out var key))
        {
            if (node is JsonObject directObject)
            {
                directObject[propertyName] = new JsonArray(x, y);
            }

            return;
        }

        if (pointNode is JsonArray array)
        {
            if (array.Count == 0)
            {
                array.Add(x);
                array.Add(y);
            }
            else
            {
                array[0] = x;
                if (array.Count == 1)
                {
                    array.Add(y);
                }
                else
                {
                    array[1] = y;
                }
            }

            return;
        }

        if (pointNode is JsonObject obj)
        {
            SetOrAddNumeric(obj, "x", x);
            SetOrAddNumeric(obj, "y", y);
            return;
        }

        parentObject[key] = new JsonArray(x, y);
    }

    private static void SetFirstNumericPropertyByName(JsonNode node, string propertyName, double value)
    {
        if (!TryFindPropertyNode(node, propertyName, out var targetNode, out var parentObject, out var key))
        {
            return;
        }

        if (targetNode is JsonValue)
        {
            parentObject[key] = value;
        }
    }

    private static void SetFirstBooleanPropertyByName(JsonNode node, string propertyName, bool value)
    {
        if (!TryFindPropertyNode(node, propertyName, out var targetNode, out var parentObject, out var key))
        {
            return;
        }

        if (targetNode is JsonValue)
        {
            parentObject[key] = value;
        }
    }

    private static void SetOrAddNumeric(JsonObject obj, string keyName, double value)
    {
        var key = obj
            .Select(kvp => kvp.Key)
            .FirstOrDefault(k => string.Equals(k, keyName, StringComparison.OrdinalIgnoreCase));

        if (key is null)
        {
            obj[keyName] = value;
        }
        else
        {
            obj[key] = value;
        }
    }

    private static bool TryFindPropertyNode(JsonNode node, string propertyName, out JsonNode foundNode, out JsonObject parentObject, out string key)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                if (string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    foundNode = kvp.Value;
                    parentObject = obj;
                    key = kvp.Key;
                    return true;
                }

                if (kvp.Value is not null && TryFindPropertyNode(kvp.Value, propertyName, out foundNode, out parentObject, out key))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null && TryFindPropertyNode(item, propertyName, out foundNode, out parentObject, out key))
                {
                    return true;
                }
            }
        }

        foundNode = null;
        parentObject = null;
        key = null;
        return false;
    }

    private static JsonObject FindObjectRecursive(JsonNode node, string propertyName)
    {
        if (!TryFindPropertyNode(node, propertyName, out var foundNode, out _, out _))
        {
            return null;
        }

        return foundNode as JsonObject;
    }

    private static JsonArray FindArrayRecursive(JsonNode node, string propertyName)
    {
        if (!TryFindPropertyNode(node, propertyName, out var foundNode, out _, out _))
        {
            return null;
        }

        return foundNode as JsonArray;
    }
}
