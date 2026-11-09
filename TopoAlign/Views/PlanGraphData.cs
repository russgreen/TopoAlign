using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TopoAlign.Views;

/// <summary>What <see cref="PlanGraph"/> draws, in display units: the edited alignment, the original, and the points between.</summary>
internal sealed class PlanGraphData
{
    public static readonly PlanGraphData Empty = new(Array.Empty<Point>(), Array.Empty<Point>(), Array.Empty<Point>(), Array.Empty<Point>(), string.Empty);

    public PlanGraphData(IReadOnlyList<Point> line, IReadOnlyList<Point> original, IReadOnlyList<Point> tangents, IReadOnlyList<Point> points, string unit)
    {
        Line = line;
        Original = original;
        Tangents = tangents;
        Points = points;
        Unit = unit;
    }

    /// <summary>The edited alignment as a polyline (X = easting, Y = northing). Empty while the points can't be laid out.</summary>
    public IReadOnlyList<Point> Line { get; }

    /// <summary>The alignment as it is in the model, drawn faintly behind the edited one.</summary>
    public IReadOnlyList<Point> Original { get; }

    /// <summary>The straight lines through the points of intersection, drawn dotted so the curves can be seen against them.</summary>
    public IReadOnlyList<Point> Tangents { get; }

    /// <summary>One marker per point of intersection.</summary>
    public IReadOnlyList<Point> Points { get; }

    public string Unit { get; }
}
