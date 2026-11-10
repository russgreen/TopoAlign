using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.Sweep.Profiles;

namespace TopoAlign.Sweep.Road;

/// <summary>
/// Builds the road as smooth B-rep solids: one spline per corner of the section running along the alignment, and one
/// ruled surface between each pair of neighbouring splines. There are no edges across the road except where it is cut into pieces.
/// </summary>
internal static class SmoothRoadBuilder
{
    private static readonly ILogger log = Log.ForContext(typeof(SmoothRoadBuilder));

    /// <summary>The shortest piece the road is cut into, in feet, so the splines stay accurate.</summary>
    private const double MinChunkLength = 100.0;

    /// <summary>Points on a spline this close to a straight line are treated as one (feet).</summary>
    private const double StraightTolerance = 1e-6;

    /// <summary>
    /// Builds the road along <paramref name="stations"/>. False, with the reason, if Revit can't make a valid smooth solid
    /// (for example the section is wider than the tightest turn); the caller then falls back to another method.
    /// </summary>
    public static bool TryBuild(PathReader path, IReadOnlyList<double> stations, SectionProfile profile, RoadOptions options,
        out List<Solid> solids, out string? problem)
    {
        solids = new List<Solid>();
        problem = null;

        try
        {
            var centres = stations.Select(path.PointAt).ToList();
            var tangents = stations.Select(path.HorizontalTangentAt).ToList();

            var curvature = SmoothRoadPlan.MaxCurvature(stations, tangents.Select(t => (t.X, t.Y)).ToList());
            var halfWidth = Math.Max(Math.Abs(profile.MinU), Math.Abs(profile.MaxU));
            if (curvature * halfWidth >= 0.98)
            {
                problem = "the section is wider than the tightest curve in the alignment";
                return false;
            }

            var ring = SmoothRoadPlan.Ring(profile);
            if (ring.Count < 3)
            {
                problem = "the section has too few corners";
                return false;
            }

            var chunks = SmoothRoadPlan.Chunks(stations, path.HorizontalCurveStations(), MinChunkLength);
            foreach (var (first, last) in chunks)
            {
                var solid = BuildChunk(ring, centres, tangents, first, last, options.VerticalOffset, out var chunkProblem);
                if (solid is null)
                {
                    problem = chunkProblem ?? "Revit could not make the surfaces";
                    solids = new List<Solid>();
                    return false;
                }

                solids.Add(solid);
            }

            return solids.Count > 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Smooth road geometry failed");
            problem = ex.Message;
            solids = new List<Solid>();
            return false;
        }
    }

    /// <summary>One smooth solid from station index <paramref name="first"/> to <paramref name="last"/>.</summary>
    internal static Solid? BuildChunk(IReadOnlyList<PlanPoint> ring, IReadOnlyList<XYZ> centres, IReadOnlyList<XYZ> tangents, int first, int last,
        double verticalOffset, out string? problem)
    {
        problem = null;
        var n = ring.Count;
        var count = last - first + 1;

        // the world position of every corner at every station of this piece
        var corners = new XYZ[n][];
        for (var v = 0; v < n; v++)
        {
            corners[v] = new XYZ[count];
            for (var s = 0; s < count; s++)
            {
                var centre = centres[first + s];
                var tangent = tangents[first + s];
                var world = SectionFrame.ToWorld(ring[v].X, ring[v].Y, new PathPoint(centre.X, centre.Y, centre.Z), tangent.X, tangent.Y, verticalOffset);
                corners[v][s] = new XYZ(world.X, world.Y, world.Z);
            }
        }

        var splines = new Curve[n];
        for (var v = 0; v < n; v++)
        {
            splines[v] = CreateSpline(corners[v]);
        }

        var startNormal = Normalize(new XYZ(-tangents[first].X, -tangents[first].Y, 0));
        var endNormal = Normalize(new XYZ(tangents[last].X, tangents[last].Y, 0));

        // The ruled surface's own sense is not documented; try it as is and, if Revit rejects the solid, the other way round.
        foreach (var reverseSides in new[] { false, true })
        {
            try
            {
                var solid = Assemble(splines, corners, startNormal, endNormal, reverseSides);
                if (solid is not null && solid.Volume > 1e-9)
                {
                    return solid;
                }

                problem = "the solid came out empty or inside out";
            }
            catch (Exception ex)
            {
                problem = ex.Message;
            }
        }

        return null;
    }

    private static Solid? Assemble(Curve[] splines, XYZ[][] corners, XYZ startNormal, XYZ endNormal, bool reverseSides)
    {
        var n = splines.Length;
        var last = corners[0].Length - 1;
        var builder = new BRepBuilder(BRepType.Solid);

        var splineEdges = new BRepBuilderGeometryId[n];
        var startEdges = new BRepBuilderGeometryId[n];
        var endEdges = new BRepBuilderGeometryId[n];
        for (var v = 0; v < n; v++)
        {
            var next = (v + 1) % n;
            splineEdges[v] = builder.AddEdge(BRepBuilderEdgeGeometry.Create(splines[v]));
            startEdges[v] = builder.AddEdge(BRepBuilderEdgeGeometry.Create(Line.CreateBound(corners[v][0], corners[next][0])));
            endEdges[v] = builder.AddEdge(BRepBuilderEdgeGeometry.Create(Line.CreateBound(corners[v][last], corners[next][last])));
        }

        for (var v = 0; v < n; v++)
        {
            var next = (v + 1) % n;
            var surface = RuledSurface.Create(splines[v], splines[next]);
            var face = builder.AddFace(BRepBuilderSurfaceGeometry.Create(surface, null), reverseSides);
            var loop = builder.AddLoop(face);
            builder.AddCoEdge(loop, splineEdges[v], false);
            builder.AddCoEdge(loop, endEdges[v], false);
            builder.AddCoEdge(loop, splineEdges[next], true);
            builder.AddCoEdge(loop, startEdges[v], true);
            builder.FinishLoop(loop);
            builder.FinishFace(face);
        }

        // the start cap faces back along the road, the end cap faces forward
        var startFace = builder.AddFace(BRepBuilderSurfaceGeometry.Create(Plane.CreateByNormalAndOrigin(startNormal, corners[0][0]), null), false);
        var startLoop = builder.AddLoop(startFace);
        for (var v = 0; v < n; v++)
        {
            builder.AddCoEdge(startLoop, startEdges[v], false);
        }

        builder.FinishLoop(startLoop);
        builder.FinishFace(startFace);

        var endFace = builder.AddFace(BRepBuilderSurfaceGeometry.Create(Plane.CreateByNormalAndOrigin(endNormal, corners[0][last]), null), false);
        var endLoop = builder.AddLoop(endFace);
        for (var v = n - 1; v >= 0; v--)
        {
            builder.AddCoEdge(endLoop, endEdges[v], true);
        }

        builder.FinishLoop(endLoop);
        builder.FinishFace(endFace);

        builder.Finish();
        return builder.IsResultAvailable() ? builder.GetResult() : null;
    }

    /// <summary>A line when the points are on one, otherwise a smooth spline through them.</summary>
    private static Curve CreateSpline(XYZ[] points)
    {
        var first = points[0];
        var end = points[points.Length - 1];
        var direction = end - first;
        var length = direction.GetLength();
        var straight = length > 1e-9;
        if (straight)
        {
            direction = direction.Divide(length);
            foreach (var point in points)
            {
                var offset = point - first;
                var along = offset.DotProduct(direction);
                if ((offset - direction.Multiply(along)).GetLength() > StraightTolerance)
                {
                    straight = false;
                    break;
                }
            }
        }

        if (straight || points.Length == 2)
        {
            return Line.CreateBound(first, end);
        }

        return HermiteSpline.Create(points, false);
    }

    private static XYZ Normalize(XYZ vector)
    {
        var length = vector.GetLength();
        if (length < 1e-12)
        {
            throw new ArgumentException("The path direction is undefined.", nameof(vector));
        }

        return vector.Divide(length);
    }
}