using System.Text.Json.Nodes;

namespace TopoAlign.Alignments;

/// <summary>
/// The JSON nodes of an alignment (the point, range and line nodes every curve is made of) and of its profile. Pure JSON,
/// no Revit types, so both <see cref="AlignmentJsonBuilder"/> and <see cref="AlignmentJsonDocument"/> can use it.
/// </summary>
internal static class ProfileJson
{
    /// <summary>
    /// Profile curve nodes for PVIs that are in the JSON's own coordinates (distances along the alignment, shared elevations),
    /// each with its own vertical curve length (the first and last entries are ignored).
    /// </summary>
    public static JsonArray CreateCurves(IReadOnlyList<ProfilePoint> pvis, IReadOnlyList<double> curveLengths)
    {
        return CreateCurves(ProfileLayout.Create(pvis, curveLengths));
    }

    /// <summary>Profile curve nodes for PVIs with one minimum vertical curve length (0 for a polyline profile).</summary>
    public static JsonArray CreateCurves(IReadOnlyList<ProfilePoint> pvis, double minVerticalCurveLength)
    {
        return CreateCurves(ProfileLayout.Create(pvis, minVerticalCurveLength));
    }

    private static JsonArray CreateCurves(IEnumerable<object> layout)
    {
        var curves = new JsonArray();
        foreach (var element in layout)
        {
            switch (element)
            {
                case ProfileLine line:
                    curves.Add(CreateLineNode(
                        line.Start.Station,
                        line.Start.Elevation,
                        line.End.Station - line.Start.Station,
                        line.End.Elevation - line.Start.Elevation));
                    break;

                case ProfileParabola parabola:
                    curves.Add(CreateVerticalCurveNode(parabola));
                    break;
            }
        }

        return curves;
    }

    public static JsonObject CreateLineNode(double x, double y, double dx, double dy)
    {
        return new JsonObject
        {
            [InfraJson.Point2DKey] = new JsonObject { ["position"] = CreatePoint(x, y) },
            [InfraJson.Vector2DKey] = new JsonObject { ["direction"] = CreatePoint(dx, dy) },
            ["String"] = new JsonObject { ["guid"] = Guid.NewGuid().ToString() },
            [InfraJson.ParamRangeKey] = new JsonObject { ["range"] = CreateRange(0.0, 0.0, 0) },
            ["typeid"] = InfraJson.LineTypeId
        };
    }

    public static JsonObject CreateVerticalCurveNode(ProfileParabola parabola)
    {
        return new JsonObject
        {
            [InfraJson.Point2DKey] = new JsonObject
            {
                ["endPoint"] = CreatePoint(parabola.End.Station, parabola.End.Elevation),
                ["pvi"] = CreatePoint(parabola.Pvi.Station, parabola.Pvi.Elevation),
                ["startPoint"] = CreatePoint(parabola.Start.Station, parabola.Start.Elevation)
            },
            ["String"] = new JsonObject { ["guid"] = Guid.NewGuid().ToString() },
            [InfraJson.ParamRangeKey] = new JsonObject { ["range"] = CreateRange(0.0, 0.0, 0) },
            ["typeid"] = InfraJson.VerticalCurveTypeId
        };
    }

    public static JsonObject CreatePoint(double x, double y)
    {
        return new JsonObject
        {
            ["Float64"] = new JsonObject { ["x"] = InfraJson.Number(x), ["y"] = InfraJson.Number(y) }
        };
    }

    public static JsonObject CreateRange(double low, double high, int type)
    {
        return new JsonObject
        {
            ["Float64"] = new JsonObject { ["high"] = InfraJson.Number(high), ["low"] = InfraJson.Number(low) },
            ["enum<Enum>"] = new JsonObject { ["type"] = type }
        };
    }
}
