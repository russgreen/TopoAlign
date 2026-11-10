using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TopoAlign.Views.Controls;

/// <summary>What <see cref="ProfileGraph"/> draws, in display units: the edited profile, the original profile and the station markers.</summary>
internal sealed class ProfileGraphData
{
    public static readonly ProfileGraphData Empty = new(Array.Empty<Point>(), Array.Empty<Point>(), Array.Empty<Point>(), string.Empty);

    public ProfileGraphData(IReadOnlyList<Point> line, IReadOnlyList<Point> original, IReadOnlyList<Point> stations, string unit)
    {
        Line = line;
        Original = original;
        Stations = stations;
        Unit = unit;
    }

    /// <summary>The edited profile as a polyline (X = station, Y = elevation). Empty while the grid isn't a valid profile.</summary>
    public IReadOnlyList<Point> Line { get; }

    /// <summary>The profile as it is in the model, drawn faintly behind the edited one.</summary>
    public IReadOnlyList<Point> Original { get; }

    /// <summary>One marker per station of the grid, at its station and elevation.</summary>
    public IReadOnlyList<Point> Stations { get; }

    public string Unit { get; }
}
