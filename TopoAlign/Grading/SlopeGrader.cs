using TopoAlign.Alignments;

namespace TopoAlign.Grading;

/// <summary>How the target slope is entered.</summary>
internal enum SlopeMode
{
    /// <summary>1 vertical to N horizontal, entered as N.</summary>
    Ratio,

    /// <summary>Rise over run as a percentage.</summary>
    Percent,

    /// <summary>Angle from horizontal in degrees.</summary>
    Degrees
}

internal static class SlopeUnits
{
    /// <summary>The slope as rise over run (0.333 for 1:3), always positive.</summary>
    public static double ToRiseOverRun(SlopeMode mode, double value)
    {
        if (!(value > 0.0) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The slope must be greater than zero.");
        }

        switch (mode)
        {
            case SlopeMode.Ratio:
                return 1.0 / value;

            case SlopeMode.Percent:
                return value / 100.0;

            case SlopeMode.Degrees:
                if (value >= 90.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The angle must be less than 90 degrees.");
                }

                return Math.Tan(value * Math.PI / 180.0);

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }
}

internal readonly struct GradePoint
{
    public GradePoint(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}

/// <summary>One point along the line being graded, and where its sloped ray meets the surface.</summary>
internal sealed class GradeStation
{
    public GradeStation(GradePoint edge, double directionX, double directionY, double perpendicularFactor)
    {
        Edge = edge;
        DirectionX = directionX;
        DirectionY = directionY;
        PerpendicularFactor = perpendicularFactor;
    }

    public GradePoint Edge { get; }

    /// <summary>Unit horizontal direction the ray travels in.</summary>
    public double DirectionX { get; }

    public double DirectionY { get; }

    /// <summary>
    /// Cosine of the angle between the ray and the line's normal: horizontal travel along the ray
    /// times this is the distance measured square to the line, which is what the slope is set against.
    /// </summary>
    public double PerpendicularFactor { get; }

    public bool Hit { get; set; }

    /// <summary>True when the ground is above the line, so the slope rises to meet it; false when it falls away.</summary>
    public bool IsCut { get; set; }

    public GradePoint Daylight { get; set; }
}

internal sealed class GradingResult
{
    public List<GradeStation> Stations { get; } = new();

    /// <summary>Plan outlines (edge i, edge i+1, daylight i+1, daylight i) of the slope between each pair of neighbouring hits.</summary>
    public List<GradePoint[]> Corridor { get; } = new();

    public int HitCount => Stations.Count(station => station.Hit);
    public int MissCount => Stations.Count - HitCount;
    public int CutCount => Stations.Count(station => station.Hit && station.IsCut);
    public int FillCount => Stations.Count(station => station.Hit && !station.IsCut);

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

    private static bool QuadContains(GradePoint[] quad, double x, double y, double tolerance)
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

/// <summary>
/// Works out where a slope leaving a line meets the ground. A ray leaves each point of the line square to it, on the side
/// that was picked, climbing if the ground is above the line and falling if it is below, until it meets the surface.
/// Everything is worked out in plan against a <see cref="TriangleSurface"/>, so it needs neither Revit geometry nor a 3D view.
/// </summary>
internal static class SlopeGrader
{
    /// <summary>Most the mitred direction at a corner may be stretched by, so a very sharp corner can't send a ray far off.</summary>
    private const double MinimumPerpendicularFactor = 0.25;

    private const double LevelTolerance = 1e-6;

    /// <param name="path">Ordered points along the line, in feet, at the spacing the surface should be shaped at.</param>
    /// <param name="closed">True if the last point joins back to the first (without repeating it).</param>
    /// <param name="sideX">Plan position of a point on the side of the line to grade towards.</param>
    /// <param name="sideY"></param>
    /// <param name="riseOverRun">The target slope; always positive.</param>
    /// <param name="maxWidth">Furthest the slope may run, measured square to the line, before giving up on a point.</param>
    /// <param name="surface">The existing ground.</param>
    /// <param name="addEdgePoints">Whether the points along the line itself are among the new points.</param>
    public static GradingResult Grade(
        IReadOnlyList<GradePoint> path,
        bool closed,
        double sideX,
        double sideY,
        double riseOverRun,
        double maxWidth,
        TriangleSurface surface,
        bool addEdgePoints)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (surface is null)
        {
            throw new ArgumentNullException(nameof(surface));
        }

        if (path.Count < 2)
        {
            throw new ArgumentException("At least two points are needed to grade from.", nameof(path));
        }

        if (!(riseOverRun > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(riseOverRun), "The slope must be greater than zero.");
        }

        if (!(maxWidth > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "The maximum width must be greater than zero.");
        }

        var result = new GradingResult();

        var normals = SegmentNormals(path, closed);
        FlipTowards(normals, path, closed, sideX, sideY);

        for (var i = 0; i < path.Count; i++)
        {
            var station = CreateStation(path, normals, closed, i);
            Trace(station, riseOverRun, maxWidth, surface);
            result.Stations.Add(station);
        }

        var count = path.Count;
        var segments = closed ? count : count - 1;
        for (var i = 0; i < segments; i++)
        {
            var from = result.Stations[i];
            var to = result.Stations[(i + 1) % count];
            if (from.Hit && to.Hit)
            {
                result.Corridor.Add(new[] { from.Edge, to.Edge, to.Daylight, from.Daylight });
            }
        }

        foreach (var station in result.Stations.Where(station => station.Hit))
        {
            if (addEdgePoints)
            {
                result.NewPoints.Add(station.Edge);
            }

            result.NewPoints.Add(station.Daylight);
        }

        return result;
    }

    /// <summary>Unit plan normal (left of travel) of each segment; the last entry belongs to the closing segment of a closed line.</summary>
    private static List<(double X, double Y)> SegmentNormals(IReadOnlyList<GradePoint> path, bool closed)
    {
        var count = path.Count;
        var segments = closed ? count : count - 1;
        var normals = new List<(double X, double Y)>(segments);
        var previous = (X: 0.0, Y: 0.0);
        var havePrevious = false;

        for (var i = 0; i < segments; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);

            if (length < 1e-9)
            {
                // a zero-length segment has no direction of its own; carry the last one on
                normals.Add(havePrevious ? previous : (0.0, 0.0));
                continue;
            }

            previous = (-dy / length, dx / length);
            havePrevious = true;
            normals.Add(previous);
        }

        // a leading run of zero-length segments takes the first real direction
        var firstReal = normals.FirstOrDefault(normal => normal.X != 0.0 || normal.Y != 0.0);
        for (var i = 0; i < normals.Count && normals[i].X == 0.0 && normals[i].Y == 0.0; i++)
        {
            normals[i] = firstReal;
        }

        if (normals.Count == 0 || (normals[0].X == 0.0 && normals[0].Y == 0.0))
        {
            throw new InvalidOperationException("The line to grade from has no length in plan.");
        }

        return normals;
    }

    /// <summary>Turns every normal round together if the picked point is on the other side of the nearest part of the line.</summary>
    private static void FlipTowards(List<(double X, double Y)> normals, IReadOnlyList<GradePoint> path, bool closed, double sideX, double sideY)
    {
        var count = path.Count;
        var bestDistance = double.MaxValue;
        var sign = 1.0;

        for (var i = 0; i < normals.Count; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared < 1e-18 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((sideX - a.X) * dx + (sideY - a.Y) * dy) / lengthSquared));
            var cx = a.X + t * dx;
            var cy = a.Y + t * dy;
            var distance = Math.Sqrt((sideX - cx) * (sideX - cx) + (sideY - cy) * (sideY - cy));

            if (distance < bestDistance - 1e-12)
            {
                bestDistance = distance;
                var side = (sideX - cx) * normals[i].X + (sideY - cy) * normals[i].Y;
                sign = side < 0.0 ? -1.0 : 1.0;
            }
        }

        if (sign < 0.0)
        {
            for (var i = 0; i < normals.Count; i++)
            {
                normals[i] = (-normals[i].X, -normals[i].Y);
            }
        }
    }

    private static GradeStation CreateStation(IReadOnlyList<GradePoint> path, List<(double X, double Y)> normals, bool closed, int index)
    {
        var count = path.Count;
        (double X, double Y) after;
        (double X, double Y) before;

        if (closed)
        {
            after = normals[index];
            before = normals[(index - 1 + count) % count];
        }
        else
        {
            after = index < normals.Count ? normals[index] : normals[normals.Count - 1];
            before = index > 0 ? normals[index - 1] : after;
        }

        // the ray bisects the two neighbouring segments' normals
        var sumX = before.X + after.X;
        var sumY = before.Y + after.Y;
        var sumLength = Math.Sqrt(sumX * sumX + sumY * sumY);

        double directionX;
        double directionY;
        if (sumLength < 1e-9)
        {
            // the line doubles back on itself
            directionX = after.X;
            directionY = after.Y;
        }
        else
        {
            directionX = sumX / sumLength;
            directionY = sumY / sumLength;
        }

        var factor = directionX * after.X + directionY * after.Y;
        factor = Math.Max(factor, MinimumPerpendicularFactor);

        return new GradeStation(path[index], directionX, directionY, factor);
    }

    /// <summary>Walks the ray out from the station until it meets the ground, then closes in on the crossing.</summary>
    private static void Trace(GradeStation station, double riseOverRun, double maxWidth, TriangleSurface surface)
    {
        var edge = station.Edge;
        if (!surface.TryGetElevation(edge.X, edge.Y, out var groundAtEdge))
        {
            return;
        }

        var initial = groundAtEdge - edge.Z;
        if (Math.Abs(initial) <= LevelTolerance)
        {
            // already on the ground
            station.Hit = true;
            station.IsCut = false;
            station.Daylight = edge;
            return;
        }

        var cut = initial > 0.0;
        var climb = (cut ? 1.0 : -1.0) * riseOverRun * station.PerpendicularFactor;
        var maxDistance = maxWidth / station.PerpendicularFactor;
        var step = Math.Min(1.0, Math.Max(0.05, maxDistance / 500.0));

        // signed gap that is positive until the ray meets the ground
        double Gap(double distance, out bool onSurface)
        {
            var x = edge.X + station.DirectionX * distance;
            var y = edge.Y + station.DirectionY * distance;
            onSurface = surface.TryGetElevation(x, y, out var ground);
            return onSurface ? (cut ? ground - (edge.Z + climb * distance) : edge.Z + climb * distance - ground) : 0.0;
        }

        var low = 0.0;
        var high = 0.0;
        var found = false;
        while (high < maxDistance)
        {
            high = Math.Min(maxDistance, high + step);
            var gap = Gap(high, out var onSurface);
            if (!onSurface)
            {
                // ran off the edge of the surface
                return;
            }

            if (gap <= 0.0)
            {
                found = true;
                break;
            }

            low = high;
        }

        if (!found)
        {
            return;
        }

        for (var i = 0; i < 50; i++)
        {
            var middle = 0.5 * (low + high);
            var gap = Gap(middle, out var onSurface);
            if (onSurface && gap > 0.0)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        var distance = 0.5 * (low + high);
        station.Hit = true;
        station.IsCut = cut;
        station.Daylight = new GradePoint(
            edge.X + station.DirectionX * distance,
            edge.Y + station.DirectionY * distance,
            edge.Z + climb * distance);
    }
}
