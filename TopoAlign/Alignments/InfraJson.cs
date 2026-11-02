using System.Text.Json.Nodes;

namespace TopoAlign.Alignments;

/// <summary>
/// The keys and type ids of Autodesk's "infra" alignment JSON, shared by the code that writes alignments
/// (<see cref="AlignmentJsonBuilder"/>) and the code that edits existing ones (<see cref="AlignmentJsonDocument"/>).
/// </summary>
internal static class InfraJson
{
    public const string LineTypeId = "autodesk.infra.geometry:line2d-1.0.0";
    public const string CircleTypeId = "autodesk.infra.geometry:circle2d-1.0.0";
    public const string VerticalCurveTypeId = "autodesk.infra.geometry:verticalCurve-1.0.0";

    public const string AecAlignmentDataKey = "autodesk.infra:aecalignmentdata-1.0.0";
    public const string AlignmentKey = "autodesk.infra:alignment-1.0.0";
    public const string CurveGeometryKey = "autodesk.infra.geometry:curvegeometry-1.0.0";
    public const string HorizontalGeometryKey = "autodesk.infra:horizontalcurvegeometry2d-1.0.0";
    public const string ProfileGeometryKey = "autodesk.infra:profilecurvegeometry2d-1.0.0";
    public const string CurveArrayKey = "array<autodesk.infra.geometry:curve2d-1.0.0>";
    public const string ParamRangeKey = "autodesk.geometry:paramrange-1.0.0";
    public const string Point2DKey = "autodesk.math:point2d-1.0.0";
    public const string Vector2DKey = "autodesk.math:vector2d-1.0.0";

    /// <summary>
    /// A JSON number that is always written with a decimal point or exponent ("100.0", never "100").
    /// Autodesk's parser rejects integer tokens for Float64 values, and System.Text.Json writes a
    /// whole-number double as an integer unless the token is kept verbatim.
    /// </summary>
    public static JsonNode Number(double value)
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

        return JsonNode.Parse(text)!;
    }
}
