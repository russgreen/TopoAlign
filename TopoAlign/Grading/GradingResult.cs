using TopoAlign.Alignments;

namespace TopoAlign.Grading;

internal sealed class GradingResult
{
    public List<GradeStation> Stations { get; } = new();

    /// <summary>Plan outlines (edge i, edge i+1, daylight i+1, daylight i) of the slope between each pair of neighbouring hits.</summary>
    public List<GradePoint[]> Corridor { get; } = new();

    public int HitCount => Stations.Count(station => station.Hit);
    public int MissCount => Stations.Count - HitCount;
    public int CutCount => Stations.Count(station => station.Hit && station.IsCut);
    public int FillCount => Stations.Count(station => station.Hit && !station.IsCut);

    /// <summary>Stations whose slope ends on the edge of the surface instead of meeting the ground (counted in <see cref="HitCount"/> too).</summary>
    public int MissCountFor(MissReason reason) => Stations.Count(station => !station.Hit && station.Miss == reason);

    public int ClippedCount => Stations.Count(station => station.Clipped);

    /// <summary>Points to put in the surface: the line itself, where wanted, and every place the slope meets the ground.</summary>
    public List<GradePoint> NewPoints { get; } = new();

    /// <summary>True when (x, y) is on or inside the graded slope, so any existing surface point there should be replaced.</summary>
    public bool Contains(double x, double y, double tolerance)
    {
        foreach (var quad in Corridor)
        {
            if (QuadContains(quad, x, y, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when (x, y) is inside the plan outline, or within the tolerance of its edge. The outline may have any number of corners.</summary>
    internal static bool QuadContains(GradePoint[] quad, double x, double y, double tolerance)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var point in quad)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        if (x < minX - tolerance || x > maxX + tolerance || y < minY - tolerance || y > maxY + tolerance)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = quad.Length - 1; i < quad.Length; j = i++)
        {
            var a = quad[i];
            var b = quad[j];

            if (DistanceToSegment(x, y, a.X, a.Y, b.X, b.Y) <= tolerance)
            {
                return true;
            }

            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared < 1e-18 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((px - ax) * dx + (py - ay) * dy) / lengthSquared));
        var cx = ax + t * dx;
        var cy = ay + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }
}
