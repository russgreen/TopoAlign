using TopoAlign.Alignments;

namespace TopoAlign.Grading;

/// <summary>
/// Works out the points for a dropped kerb: a ramp the width of the drop that falls to the level of the road's edge at the
/// kerb, and a flare each side of it that brings the kerb line back up to its own level. All in plan against a
/// <see cref="TriangleSurface"/>, so it needs no Revit geometry.
/// </summary>
internal static class KerbRamp
{
    /// <summary>A kerb that turns more than this inside the ramp or a flare is too sharp for them to follow.</summary>
    public const double MaxCornerRadians = 45.0 * Math.PI / 180.0;

    // less drop than this is no drop
    private const double LevelTolerance = 1e-4;

    // how far in from the kerb to look for the surface when working out which side the footway is on
    private const double SideProbe = 0.05;

    public static KerbRampResult Build(KerbRampInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!(input.Width > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "The width of the drop must be greater than zero.");
        }

        if (!(input.RampRatio > 0.0) || !(input.FlareRatio > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "The ramp and flare gradients must be greater than zero.");
        }

        if (!(input.MaxRampLength > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "The maximum ramp length must be greater than zero.");
        }

        var kerb = KerbPath.FromSegments(input.KerbOutline, input.KerbAnchorX, input.KerbAnchorY);
        var target = KerbPath.FromSegments(input.TargetOutline, input.TargetAnchorX, input.TargetAnchorY);

        kerb.Project(input.CentreX, input.CentreY, out var centre, out _);
        var half = input.Width / 2.0;
        var from = centre - half;
        var to = centre + half;

        if (!kerb.IsClosed && (from < -1e-6 || to > kerb.Length + 1e-6))
        {
            throw new InvalidOperationException(
                "The dropped kerb runs off the end of the edge. Move the centre point along the edge or make the width smaller.");
        }

        foreach (var vertex in kerb.VertexPositions(from, to))
        {
            if (kerb.TurnAt(vertex) > MaxCornerRadians)
            {
                throw new InvalidOperationException(
                    "The kerb turns a sharp corner inside the dropped section. Move the centre point along the edge or make the width smaller.");
            }
        }

        // the ramp stations, at the level of the road's edge
        var path = kerb.Sample(from, to, input.Spacing)
            .Select(point => new GradePoint(point.X, point.Y, LevelAt(target, point.X, point.Y) + input.LevelOffset))
            .ToList();

        var side = FootwaySide(kerb, centre, input.Surface);
        var ramp = SlopeGrader.Grade(path, false, side.X, side.Y, 1.0 / input.RampRatio, input.MaxRampLength, input.Surface, true);
        if (ramp.MissCount > 0)
        {
            throw new InvalidOperationException(
                $"The ramp does not meet the footway within {input.MaxRampLength:0.##} ft of the kerb at {ramp.MissCount} of {ramp.Stations.Count} points. " +
                "Increase the maximum ramp length, or check the target edge is the one you meant.");
        }

        var result = new KerbRampResult { Stations = ramp.Stations.Count };
        result.NewPoints.AddRange(ramp.NewPoints);
        result.ClearPolygons.AddRange(ramp.Corridor);
        result.RampLength = ramp.Stations.Max(station => Distance(station.Edge, station.Daylight) * station.PerpendicularFactor);

        // the ramp's first station is at the start of the window and its last at the end
        result.StartFlareLength = AddFlare(result, kerb, from, -1, ramp.Stations[0], input.FlareRatio);
        result.EndFlareLength = AddFlare(result, kerb, to, +1, ramp.Stations[ramp.Stations.Count - 1], input.FlareRatio);

        return result;
    }

    /// <summary>The height of the path at the point on it nearest (x, y).</summary>
    private static double LevelAt(KerbPath path, double x, double y)
    {
        path.Project(x, y, out var position, out _);
        return path.PointAt(position).Z;
    }

    /// <summary>A point a little way out on the side of the kerb that has surface, which is the side the footway is on.</summary>
    private static (double X, double Y) FootwaySide(KerbPath kerb, double position, TriangleSurface surface)
    {
        var point = kerb.PointAt(position);
        var direction = kerb.DirectionAt(position);
        var left = (X: -direction.Y, Y: direction.X);

        var onLeft = surface.TryGetElevation(point.X + left.X * SideProbe, point.Y + left.Y * SideProbe, out _);
        var onRight = surface.TryGetElevation(point.X - left.X * SideProbe, point.Y - left.Y * SideProbe, out _);
        if (!onLeft && !onRight)
        {
            throw new InvalidOperationException("The picked edge is not on the surface of the toposolid being cut.");
        }

        var sign = onLeft ? 1.0 : -1.0;
        return (point.X + sign * left.X, point.Y + sign * left.Y);
    }

    /// <summary>
    /// Adds the flare at one end of the ramp and returns how far along the kerb it runs.
    /// The kerb line climbs from the ramp's end at the flare gradient until it is back at its own level. The flare is the
    /// surface between that stretch of kerb and the top corner of the ramp's side.
    /// </summary>
    private static double AddFlare(KerbRampResult result, KerbPath kerb, double start, int direction, GradeStation end, double flareRatio)
    {
        var low = end.Edge.Z;
        var existing = kerb.PointAt(start).Z;
        if (existing - low <= LevelTolerance)
        {
            // the kerb is no higher than the ramp here, so there is nothing to flare
            return 0.0;
        }

        var available = kerb.DistanceAvailable(start, direction);

        // the walk visits each corner of the kerb in turn, then the furthest it may go
        var stops = kerb.VertexPositions(direction > 0 ? start : start - available, direction > 0 ? start + available : start)
            .Select(position => Math.Abs(position - start))
            .OrderBy(distance => distance)
            .ToList();
        var vertexCount = stops.Count;
        stops.Add(available);

        var outline = new List<GradePoint> { end.Edge };
        var previousDistance = 0.0;
        var previousGap = low - existing;
        var length = 0.0;
        var closedUp = false;
        var stoppedAtCorner = false;

        for (var i = 0; i < stops.Count; i++)
        {
            var distance = stops[i];
            var onKerb = kerb.PointAt(start + direction * distance);
            var level = low + distance / flareRatio;
            var gap = level - onKerb.Z;

            if (gap >= 0.0)
            {
                // the flare meets the kerb's own level between the last stop and this one
                var t = previousGap >= 0.0 ? 0.0 : -previousGap / (gap - previousGap);
                var crossing = previousDistance + t * (distance - previousDistance);
                var meet = kerb.PointAt(start + direction * crossing);
                outline.Add(meet);
                length = crossing;
                closedUp = true;
                break;
            }

            var flarePoint = new GradePoint(onKerb.X, onKerb.Y, level);
            outline.Add(flarePoint);
            length = distance;
            previousDistance = distance;
            previousGap = gap;

            var isCorner = i < vertexCount;
            if (isCorner && kerb.TurnAt(start + direction * distance) > MaxCornerRadians)
            {
                result.Warnings.Add($"The {(direction < 0 ? "first" : "second")} flare stops at a sharp corner in the kerb before it has met the kerb's level.");
                stoppedAtCorner = true;
                break;
            }
        }

        if (!closedUp && !stoppedAtCorner)
        {
            result.Warnings.Add($"The {(direction < 0 ? "first" : "second")} flare reaches the end of the edge before it has met the kerb's level.");
        }

        // everything after the ramp's own end station is a new point; the end station itself is already one
        result.NewPoints.AddRange(outline.Skip(1));

        outline.Add(end.Daylight);
        result.ClearPolygons.Add(outline.ToArray());
        return length;
    }

    private static double Distance(GradePoint a, GradePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}