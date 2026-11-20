using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.Grading;
using TopoAlign.Sweep.Profiles;
#if REVIT2024_OR_GREATER
using TopoAlign.Comparers;
using TopoAlign.Grading.GradeToSurface;
using TopoAlign.Grading.TopoEdge;
#endif

namespace TopoAlign.Sweep.Road;

/// <summary>
/// Shapes a toposolid to the underside of the road: points are added along the bottom edge of the road elements at every section,
/// and the toposolid's own points inside the road are removed, so the surface follows the base of the road instead of passing through it.
/// Toposolids need Revit 2024 or later.
/// </summary>
internal static class RoadBasePoints
{
    private static readonly ILogger log = Log.ForContext(typeof(RoadBasePoints));

    /// <summary>Where the bottom steps down or up, the second point is moved this far across the road (feet) so the two do not share a plan position.</summary>
    private const double StepSeparation = 0.06;

    /// <summary>How far in from the road's outer edges the outermost base points are put (feet, about 15 mm), the smallest step Revit takes reliably.</summary>
    private const double EdgeInset = 0.05;

    /// <summary>How far in from the road's outer edges the ground points that get cleared start (feet), so points on the edge are never cleared.</summary>
    private const double EdgeClearance = 0.15;

    /// <summary>Points closer than this in plan (feet) are not both used; the toposolid cannot hold them.</summary>
    private const double MinimumSeparation = 0.03;

    /// <summary>Points on the road's base, as a loop in plan, and the points to give the toposolid.</summary>
    internal static bool TryBuild(IReadOnlyList<SectionProfile> profiles, IReadOnlyList<double> stations, PathReader path, double verticalOffset,
        out List<GradePoint> points, out List<(double X, double Y)> footprint, out string? error)
    {
        points = new List<GradePoint>();
        footprint = new List<(double X, double Y)>();
        error = null;

        if (profiles is null || profiles.Count == 0 || stations is null || stations.Count < 2)
        {
            error = "There is no road base to shape the topo to.";
            return false;
        }

        var bottom = BottomEdge(profiles.Select(p => p.Outline()).ToList());
        if (bottom.Count < 2)
        {
            error = "The road has no bottom edge to shape the topo to.";
            return false;
        }

        // two points at the same distance across (a vertical step in the base) are moved apart
        var section = new List<(double U, double V)>(bottom.Count);
        foreach (var point in bottom)
        {
            var u = point.X;
            if (section.Count > 0 && u - section[section.Count - 1].U < StepSeparation)
            {
                u = section[section.Count - 1].U + StepSeparation;
            }

            section.Add((u, point.Y));
        }

        var minU = section[0].U;
        var maxU = section[section.Count - 1].U;

        // The outermost points sit on the edge of the road, where the ground outside is at a different height. They are moved in
        // towards the centre so the ground's points keep their own plan position instead of the two fighting over one.
        if (section.Count >= 4)
        {
            section[0] = (section[0].U + EdgeInset, section[0].V);
            section[section.Count - 1] = (section[section.Count - 1].U - EdgeInset, section[section.Count - 1].V);
        }

        // The ground points to clear stop short of the road's outer edges. The grading's edge points lie right on those edges, and a
        // point exactly on the boundary of the footprint is removed or kept by chance, which leaves gaps along the edge.
        var clearMin = minU + EdgeClearance;
        var clearMax = maxU - EdgeClearance;
        if (clearMin >= clearMax)
        {
            clearMin = (minU + maxU) / 2.0 - 0.001;
            clearMax = (minU + maxU) / 2.0 + 0.001;
        }

        var left = new List<(double X, double Y)>(stations.Count);
        var right = new List<(double X, double Y)>(stations.Count);
        var index = new PlanIndex(MinimumSeparation);

        foreach (var station in stations)
        {
            var centre = path.PointAt(station);
            var tangent = path.HorizontalTangentAt(station);
            var centrePoint = new PathPoint(centre.X, centre.Y, centre.Z);

            var l = SectionFrame.ToWorld(clearMin, 0.0, centrePoint, tangent.X, tangent.Y, 0.0);
            var r = SectionFrame.ToWorld(clearMax, 0.0, centrePoint, tangent.X, tangent.Y, 0.0);
            left.Add((l.X, l.Y));
            right.Add((r.X, r.Y));

            foreach (var (u, v) in section)
            {
                var world = SectionFrame.ToWorld(u, v, centrePoint, tangent.X, tangent.Y, verticalOffset);
                if (index.TryAdd(world.X, world.Y))
                {
                    points.Add(new GradePoint(world.X, world.Y, world.Z));
                }
            }
        }

        footprint.AddRange(left);
        for (var i = right.Count - 1; i >= 0; i--)
        {
            footprint.Add(right[i]);
        }

        return points.Count > 0;
    }

    /// <summary>Distance either side of a vertex at which the bottom edge is read, so a step in the profile is picked up, in feet.</summary>
    private const double Probe = 1e-4;

    /// <summary>Distances across the road closer than this, in feet, are the same.</summary>
    private const double Merge = 1e-3;

    /// <summary>The lower boundary of all the profiles from left to right, as points in (u, v).</summary>
    private static List<PlanPoint> BottomEdge(IReadOnlyList<List<PlanPoint>> outlines)
    {
        var us = outlines.SelectMany(o => o).Select(p => p.X).OrderBy(u => u).ToList();
        var distinct = new List<double>();
        foreach (var u in us)
        {
            if (distinct.Count == 0 || u - distinct[distinct.Count - 1] > Merge)
            {
                distinct.Add(u);
            }
        }

        var points = new List<PlanPoint>();
        for (var i = 0; i < distinct.Count; i++)
        {
            var u = distinct[i];
            var left = LowestAt(outlines, u - Probe);
            var right = LowestAt(outlines, u + Probe);

            if (left is { } l)
            {
                points.Add(new PlanPoint(u, l));
            }

            if (right is { } r && (left is null || Math.Abs(r - left.Value) > Probe))
            {
                points.Add(new PlanPoint(u, r));
            }

            // a point between this vertex and the next picks up where one profile's edge passes under another's
            if (i < distinct.Count - 1)
            {
                var middle = (u + distinct[i + 1]) / 2.0;
                if (LowestAt(outlines, middle) is { } m)
                {
                    points.Add(new PlanPoint(middle, m));
                }
            }
        }

        return points;
    }

    /// <summary>The lowest height of any profile at <paramref name="u"/>, or null where no profile reaches that far across.</summary>
    private static double? LowestAt(IReadOnlyList<List<PlanPoint>> outlines, double u)
    {
        double? lowest = null;
        foreach (var outline in outlines)
        {
            for (var i = 0; i < outline.Count; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Count];
                var low = Math.Min(a.X, b.X);
                var high = Math.Max(a.X, b.X);
                if (u < low || u > high || high - low < 1e-9)
                {
                    continue;
                }

                var v = a.Y + (b.Y - a.Y) * (u - a.X) / (b.X - a.X);
                if (lowest is null || v < lowest.Value)
                {
                    lowest = v;
                }
            }
        }

        return lowest;
    }

    /// <summary>True if (x, y) is inside the loop.</summary>
    internal static bool Inside(IReadOnlyList<(double X, double Y)> loop, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
        {
            var a = loop[i];
            var b = loop[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// Shapes the toposolids to the base of the road. Never throws for a problem with the shaping itself: the road is already in the
    /// model, so the problem comes back as text. Must be called from within the Revit API context; each toposolid has its own transaction.
    /// </summary>
    /// <param name="profiles">The profiles as swept (mirrored and offset already).</param>
    /// <param name="toposolids">The toposolids to shape. Null or empty shapes every toposolid the road touches.</param>
    public static string Apply(Document document, PathReader path, IReadOnlyList<double> stations, IReadOnlyList<SectionProfile> profiles,
        RoadOptions options, IReadOnlyList<Element>? toposolids)
    {
#if REVIT2024_OR_GREATER
        try
        {
            if (!TryBuild(profiles, stations, path, options.VerticalOffset, out var points, out var footprint, out var error))
            {
                return $"The topo was not shaped to the base of the road: {error}";
            }

            var targets = FindTargets(document, points, toposolids);
            if (targets.Count == 0)
            {
                return "The topo was not shaped to the base of the road: no toposolid was found where the road is.";
            }

            var shaped = 0;
            var added = 0;
            var problems = new List<string>();
            foreach (var target in targets)
            {
                try
                {
                    added += ShapeOne(document, target, points, footprint);
                    shaped++;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                {
                    log.Warning(ex, "Shaping toposolid {toposolid} to the road base failed", target.Id);
                    problems.Add(ex.Message);
                }
            }

            var text = shaped == 0
                ? "The topo was not shaped to the base of the road."
                : $"Shaped {shaped} toposolid{(shaped == 1 ? string.Empty : "s")} to the base of the road ({added} points added).";
            return problems.Count == 0 ? text : $"{text} {string.Join("; ", problems.Distinct())}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            log.Warning(ex, "Shaping the topo to the road base failed");
            return $"The topo was not shaped to the base of the road: {ex.Message}";
        }
#else
        return "The topo was not shaped to the base of the road: toposolids need Revit 2024 or later.";
#endif
    }

#if REVIT2024_OR_GREATER
    /// <summary>Replaces the toposolid's points inside the road with the road base. Returns how many points were added.</summary>
    private static int ShapeOne(Document document, Toposolid toposolid, IReadOnlyList<GradePoint> points, IReadOnlyList<(double X, double Y)> footprint)
    {
        var outline = TopoEdgeAlignerRevit.ReadOutline(toposolid);
        var split = BoundaryPoints.Split(points, outline);

        var separation = new PlanIndex(MinimumSeparation);
        var moves = new List<GradePoint>();
        foreach (var corner in split.MoveCorner)
        {
            if (separation.TryAdd(corner.X, corner.Y))
            {
                moves.Add(corner);
            }
        }

        var onEdge = split.OnEdge.Where(p => separation.TryAdd(p.X, p.Y)).ToList();
        var interior = split.Add.Where(p => separation.TryAdd(p.X, p.Y)).ToList();
        if (interior.Count + onEdge.Count + moves.Count == 0)
        {
            throw new InvalidOperationException("the road does not reach the toposolid");
        }

        // existing points inside the road, or where a new point takes their place, go; everything else stays
        var kept = new List<XYZ>();
        foreach (SlabShapeVertex vertex in toposolid.GetSlabShapeEditor().SlabShapeVertices)
        {
            var position = vertex.Position;
            if (Inside(footprint, position.X, position.Y) || separation.HasNear(position.X, position.Y))
            {
                continue;
            }

            kept.Add(new XYZ(position.X, position.Y, position.Z));
        }

        var levelPoints = interior.Select(p => new XYZ(p.X, p.Y, p.Z)).Concat(kept).Distinct(new XyzEqualityComparer()).ToList();
        var name = "Shape topo to road base";

        try
        {
            ToposolidGrader.ReplacePoints(document, toposolid, levelPoints, name, onEdge, moves, outline);
        }
        catch (CornerMovesFailedException)
        {
            ToposolidGrader.ReplacePoints(document, toposolid, levelPoints, name, onEdge.Concat(moves).ToList(), new List<GradePoint>(), outline);
        }
        catch (PointsRejectedException)
        {
            // Revit would not build the shape: try again with only the points well inside the toposolid
            var safe = interior.Where(p => BoundaryPoints.DistanceToOutline(p, outline) >= 0.3).ToList();
            var safePoints = safe.Select(p => new XYZ(p.X, p.Y, p.Z)).Concat(kept).Distinct(new XyzEqualityComparer()).ToList();
            ToposolidGrader.ReplacePoints(document, toposolid, safePoints, name, new List<GradePoint>(), new List<GradePoint>(), outline);
            return safe.Count;
        }

        return interior.Count + onEdge.Count + moves.Count;
    }

    /// <summary>The toposolids picked, or every toposolid whose box meets the road's points.</summary>
    private static List<Toposolid> FindTargets(Document document, IReadOnlyList<GradePoint> points, IReadOnlyList<Element>? picked)
    {
        var chosen = picked?.OfType<Toposolid>().Where(t => t.IsValidObject).ToList();
        if (chosen is { Count: > 0 })
        {
            return chosen;
        }

        var outline = new Outline(
            new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z) - 1000.0),
            new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z) + 1000.0));
        return new FilteredElementCollector(document)
            .OfClass(typeof(Toposolid))
            .WherePasses(new BoundingBoxIntersectsFilter(outline))
            .Cast<Toposolid>()
            .ToList();
    }
#endif
}
