#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>What the dialog hands back to the command.</summary>
internal sealed class GradeToSurfaceRequest
{
    public GradeToSurfaceRequest(
        Toposolid toposolid,
        IReadOnlyList<Curve> curves,
        XYZ sidePoint,
        SlopeMode slopeMode,
        double slopeValue,
        double maxWidth,
        double verticalOffset,
        double interval,
        bool addEdgePoints)
    {
        Toposolid = toposolid;
        Curves = curves;
        SidePoint = sidePoint;
        SlopeMode = slopeMode;
        SlopeValue = slopeValue;
        MaxWidth = maxWidth;
        VerticalOffset = verticalOffset;
        Interval = interval;
        AddEdgePoints = addEdgePoints;
    }

    public Toposolid Toposolid { get; }

    /// <summary>The line to grade from, in order and end to end.</summary>
    public IReadOnlyList<Curve> Curves { get; }

    public XYZ SidePoint { get; }
    public SlopeMode SlopeMode { get; }
    public double SlopeValue { get; }

    /// <summary>All lengths are in feet.</summary>
    public double MaxWidth { get; }

    public double VerticalOffset { get; }
    public double Interval { get; }
    public bool AddEdgePoints { get; }
}

internal sealed class GradeToSurfaceResult
{
    public int Stations { get; set; }
    public int Cut { get; set; }
    public int Fill { get; set; }
    public int Missed { get; set; }

    /// <summary>Of the missed points, how many are not over the toposolid.</summary>
    public int MissedOffSurface { get; set; }

    /// <summary>Of the missed points, how many did not meet the ground within the maximum width.</summary>
    public int MissedTooWide { get; set; }

    /// <summary>Of the missed points, how many have a slope that leaves the toposolid straight away.</summary>
    public int MissedAtEdge { get; set; }

    /// <summary>Points along the line whose slope ran off the toposolid's edge before meeting the ground, so it ends on the edge. Counted in cut/fill too.</summary>
    public int Clipped { get; set; }

    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }

    /// <summary>0 if everything went in; 1 if Revit rejected the shape and it was redone without edge points; 2 if it was redone with only well-spaced points clear of the edge.</summary>
    public int FallbackLevel { get; set; }

    /// <summary>How many points were left out by that fall-back.</summary>
    public int PointsDroppedForRetry { get; set; }

    /// <summary>New points left out because they were too close to another new point (typically on the inside of a bend).</summary>
    public int PointsThinned { get; set; }

    /// <summary>Points on the toposolid's edge that were placed just inside it, because Revit would not take them on the edge.</summary>
    public int PointsInset { get; set; }

    /// <summary>Corner points of the toposolid that were set to the graded height instead of being added.</summary>
    public int CornersMoved { get; set; }

    /// <summary>New points left out because they were outside the toposolid or Revit would not take them.</summary>
    public int PointsSkipped { get; set; }

    /// <summary>Of the skipped points, how many were outside the toposolid's outline, and the furthest of them (feet).</summary>
    public int PointsOutside { get; set; }

    public double FurthestOutside { get; set; }

    /// <summary>Of the skipped points, how many Revit refused to add.</summary>
    public int PointsRefused { get; set; }

    /// <summary>Of the skipped points, how many were corners or edge points whose height could not be set.</summary>
    public int MovesFailed { get; set; }

    /// <summary>Why the corner heights could not be set, when they could not.</summary>
    public string MoveFailure { get; set; } = string.Empty;
}

/// <summary>Revit would not build a shape from the points, and the toposolid was left as it was.</summary>
internal sealed class PointsRejectedException : InvalidOperationException
{
    public PointsRejectedException(string message)
        : base(message)
    {
    }
}

/// <summary>What <see cref="ToposolidGrader.ReplacePoints"/> could not do.</summary>
internal sealed class ReplacePointsOutcome
{
    /// <summary>Points from the "try one at a time" list that Revit took.</summary>
    public int TrialAdded { get; set; }

    /// <summary>Of those, how many had to go just inside the toposolid's edge because Revit would not take a point on the edge itself.</summary>
    public int Inset { get; set; }

    /// <summary>Points Revit refused.</summary>
    public int Refused { get; set; }

    /// <summary>Vertex moves that could not be made.</summary>
    public int MovesFailed { get; set; }

    /// <summary>Why the moves failed, when they did.</summary>
    public string MoveFailure { get; set; } = string.Empty;
}

/// <summary>Grades a slope from a line into a toposolid by replacing the surface points between the line and where the slope meets the ground.</summary>
internal static class ToposolidGrader
{
    private static readonly ILogger log = Log.ForContext(typeof(ToposolidGrader));

    // a vertex this close (in plan) to the slope's outline counts as part of it
    private const double PlanTolerance = 1.0 / (16.0 * 12.0);

    /// <summary>
    /// Works out the slope, then rebuilds the toposolid's points in one transaction. Must be called from within the Revit API context.
    /// </summary>
    public static GradeToSurfaceResult Apply(Document document, GradeToSurfaceRequest request)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var toposolid = request.Toposolid;
        var surface = TopographyReader.Read(toposolid);

        var path = BuildPath(request.Curves, request.Interval, request.VerticalOffset, out var closed);
        var slope = SlopeUnits.ToRiseOverRun(request.SlopeMode, request.SlopeValue);
        var grading = SlopeGrader.Grade(path, closed, request.SidePoint.X, request.SidePoint.Y, slope, request.MaxWidth, surface, request.AddEdgePoints);

        var result = new GradeToSurfaceResult
        {
            Stations = grading.Stations.Count,
            Cut = grading.CutCount,
            Fill = grading.FillCount,
            Missed = grading.MissCount,
            MissedOffSurface = grading.MissCountFor(MissReason.LineOffSurface),
            MissedTooWide = grading.MissCountFor(MissReason.NoGroundWithinWidth),
            MissedAtEdge = grading.MissCountFor(MissReason.LeavesAtOnce),
            Clipped = grading.ClippedCount
        };

        if (grading.Corridor.Count == 0)
        {
            throw new InvalidOperationException(
                "The slope does not meet the toposolid anywhere along the line. " +
                "Check that the side you picked is towards the toposolid, and that the maximum width is long enough.");
        }

        // Points on the toposolid's outline can't simply be added: a corner already holds a point, and Revit refuses
        // points on or beyond the edge. Sort them first so one bad point doesn't fail the whole edit.
        var outline = TopoEdgeAlignerRevit.ReadOutline(toposolid);
        var split = BoundaryPoints.Split(grading.NewPoints, outline);
        result.PointsOutside = split.Outside.Count;
        result.PointsSkipped = split.Outside.Count;
        foreach (var outside in split.Outside)
        {
            result.FurthestOutside = Math.Max(result.FurthestOutside, BoundaryPoints.DistanceToOutline(outside, outline));
        }

        result.CornersMoved = split.MoveCorner.Count;

        // A toposolid can't hold points that nearly coincide, and on the inside of a bend the slopes from neighbouring points bunch up.
        // So a new point too close to one already chosen is dropped; corners come first, then edge points, then the rest.
        var separation = new PlanIndex(MinimumSeparation);
        foreach (var corner in split.MoveCorner)
        {
            separation.Add(corner.X, corner.Y);
        }

        var moves = new List<GradePoint>(split.MoveCorner);
        var onEdge = new List<GradePoint>();
        foreach (var point in split.OnEdge)
        {
            if (separation.TryAdd(point.X, point.Y))
            {
                onEdge.Add(point);
            }
            else
            {
                result.PointsThinned++;
            }
        }

        var interior = new List<GradePoint>();
        foreach (var point in split.Add)
        {
            if (separation.TryAdd(point.X, point.Y))
            {
                interior.Add(point);
            }
            else
            {
                result.PointsThinned++;
            }
        }

        // everything in the surface that isn't being replaced goes back in
        var kept = new List<XYZ>();
        foreach (SlabShapeVertex vertex in toposolid.GetSlabShapeEditor().SlabShapeVertices)
        {
            var position = vertex.Position;
            if (grading.Contains(position.X, position.Y, PlanTolerance))
            {
                result.PointsRemoved++;
            }
            else if (separation.HasNear(position.X, position.Y))
            {
                // a new point (or a corner set to its new height) takes this one's place
                result.PointsRemoved++;
            }
            else
            {
                kept.Add(new XYZ(position.X, position.Y, position.Z));
            }
        }

        // the new points come first so they win over an existing point within the duplicate tolerance
        var points = interior
            .Select(point => new XYZ(point.X, point.Y, point.Z))
            .Concat(kept)
            .Distinct(new XyzEqualityComparer())
            .ToList();
        result.PointsAdded = points.Count - kept.Count;

        // Revit can accept every point and still fail to build the shape from them, which it only finds out when it regenerates.
        // A failed attempt is rolled back completely, so try again with fewer, safer points: first without the edge points,
        // then without the corner heights too, and only well clear of the edge and further apart.
        PointsRejectedException? rejected = null;
        for (var level = 0; level <= 2; level++)
        {
            var levelInterior = level < 2 ? interior : SafeInterior(interior, outline);
            var levelPoints = levelInterior
                .Select(point => new XYZ(point.X, point.Y, point.Z))
                .Concat(kept)
                .Distinct(new XyzEqualityComparer())
                .ToList();
            var levelOnEdge = level == 0 ? onEdge : new List<GradePoint>();
            var levelMoves = level < 2 ? moves : new List<GradePoint>();

            try
            {
                var outcome = ReplacePoints(document, toposolid, levelPoints, "Grade to surface", levelOnEdge, levelMoves, outline);
                result.FallbackLevel = level;
                result.PointsAdded = levelPoints.Count - kept.Count + outcome.TrialAdded;
                result.PointsInset = outcome.Inset;
                result.PointsRefused = outcome.Refused;
                result.MovesFailed = outcome.MovesFailed;
                result.MoveFailure = outcome.MoveFailure;
                result.CornersMoved = level < 2 ? Math.Max(0, split.MoveCorner.Count - outcome.MovesFailed) : 0;
                result.PointsSkipped += outcome.Refused + outcome.MovesFailed;
                result.PointsDroppedForRetry = (onEdge.Count - levelOnEdge.Count) + (interior.Count - levelInterior.Count);
                return result;
            }
            catch (PointsRejectedException ex)
            {
                log.Warning(ex, "Revit rejected the shape at fallback level {level}", level);
                rejected = ex;
            }
        }

        throw rejected!;
    }

    // the safest points: well inside the toposolid and well apart, so Revit has nothing awkward to triangulate
    private const double SafeEdgeClearance = 0.3;
    private const double SafeSeparation = 0.25;

    private static List<GradePoint> SafeInterior(IReadOnlyList<GradePoint> points, IReadOnlyList<EdgeSegment> outline)
    {
        var index = new PlanIndex(SafeSeparation);
        var safe = new List<GradePoint>();
        foreach (var point in points)
        {
            if (BoundaryPoints.DistanceToOutline(point, outline) >= SafeEdgeClearance && index.TryAdd(point.X, point.Y))
            {
                safe.Add(point);
            }
        }

        return safe;
    }

    // points along a curve are never put closer than this (feet), whatever the spacing
    private const double MinimumPointSpacing = 0.25;

    // new points closer than this (in plan) to each other, or to an existing point, are left out: about 15 mm
    private const double MinimumSeparation = 0.05;

    /// <summary>
    /// Gives the toposolid these points, in one transaction. Must be called from within the Revit API context.
    /// </summary>
    /// <param name="points">Points that are expected to go in. If Revit refuses the batch they are added one at a time, and refused ones are skipped.</param>
    /// <param name="tryPoints">Points that may be refused (such as on the toposolid's edge); each is tried alone and skipped if refused.</param>
    /// <param name="moves">Boundary corners to set to the height of these points, before the other points go in.</param>
    internal static ReplacePointsOutcome ReplacePoints(
        Document document,
        Toposolid toposolid,
        IReadOnlyList<XYZ> points,
        string transactionName,
        IReadOnlyList<GradePoint>? tryPoints = null,
        IReadOnlyList<GradePoint>? moves = null,
        IReadOnlyList<EdgeSegment>? outline = null)
    {
        var outcome = new ReplacePointsOutcome();

        using var transaction = new Transaction(document, transactionName);
        transaction.Start();

        // an error is undone quietly, so Revit's own error box (which only offers to reset the toposolid) never appears
        var failureHandler = new FailureHandler(rollBackOnError: true);
        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(failureHandler);
        failureHandlingOptions.SetClearAfterRollback(true);
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        try
        {
            var editor = toposolid.GetSlabShapeEditor();
            editor.ResetSlabShape();
            editor.Enable();
            document.Regenerate();

            // The toposolid's own corners are set first, while it is still just its boundary, the way Align Edges does it.
            // This is in a sub-transaction so that if Revit objects, only the corner heights are undone and the rest goes ahead.
            if (moves is { Count: > 0 })
            {
                using var subTransaction = new SubTransaction(document);
                subTransaction.Start();
                try
                {
                    TopoEdgeAlignerRevit.MoveVertices(document, editor, moves);
                    subTransaction.Commit();
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "{transaction} could not set the height of every point on the toposolid's edge", transactionName);
                    if (subTransaction.HasStarted())
                    {
                        subTransaction.RollBack();
                    }

                    outcome.MovesFailed += moves.Count;
                    outcome.MoveFailure = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
                }
            }

            // Every attempt is in its own sub-transaction: when Revit refuses a point it also posts a "slab shape edit failed" error
            // that can only be cleared by resetting the toposolid, and rolling the sub-transaction back removes that error.
            if (!Attempt(document, () => editor.AddPoints(points.ToList()), "all points"))
            {
                // one point Revit doesn't like fails the whole batch, so find which ones it does take
                log.Warning("{transaction} could not add all points together; adding them one at a time", transactionName);
                var refused = 0;
                foreach (var point in points)
                {
                    if (!TryAdd(document, editor, point))
                    {
                        refused++;
                    }
                }

                outcome.Refused += refused;
                if (refused == points.Count && points.Count > 0)
                {
                    throw new PointsRejectedException("Revit would not add any of the new points to the toposolid.");
                }
            }

            if (tryPoints is not null)
            {
                foreach (var point in tryPoints)
                {
                    AddEdgePoint(document, editor, point, outline, outcome);
                }
            }

            // The command regenerates manually, so Revit would otherwise only build the new shape after the command has finished,
            // when a failure can no longer be rolled back and Revit's own error box (which only offers to reset the toposolid) appears.
            try
            {
                document.Regenerate();
            }
            catch (Exception ex)
            {
                throw new PointsRejectedException($"Revit could not build the shape from the new points. {ex.Message}");
            }

            if (transaction.Commit() != TransactionStatus.Committed)
            {
                var reason = string.IsNullOrWhiteSpace(failureHandler.ErrorMessage) ? string.Empty : $" Revit said: {failureHandler.ErrorMessage}";
                throw new PointsRejectedException(
                    "Revit would not accept the new points, so the toposolid has not been changed." + reason +
                    " Try a larger spacing along the line, or pick a simpler stretch of edge.");
            }

            return outcome;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "{transaction} failed", transactionName);
            if (transaction.HasStarted())
            {
                transaction.RollBack();
            }

            throw;
        }
    }

    // how far inside the edge a point goes when Revit won't take it on the edge itself, in feet (about 15 mm)
    private const double EdgeInset = 0.05;

    /// <summary>
    /// Puts a point on the toposolid's edge. Revit refuses points on an edge in some cases, so it tries the point exactly on the edge
    /// and then just inside the edge, and counts the point as refused only if both fail.
    /// </summary>
    private static void AddEdgePoint(Document document, SlabShapeEditor editor, GradePoint point, IReadOnlyList<EdgeSegment>? outline, ReplacePointsOutcome outcome)
    {
        var onEdge = point;
        if (outline is { Count: > 0 } && BoundaryPoints.TryProjectOntoOutline(point, outline, out var projected))
        {
            onEdge = projected;
        }

        var edgeXyz = new XYZ(onEdge.X, onEdge.Y, onEdge.Z);
        if (TryAdd(document, editor, edgeXyz))
        {
            outcome.TrialAdded++;
            return;
        }

        if (outline is { Count: > 0 } && BoundaryPoints.TryInsetFromOutline(point, outline, EdgeInset, out var inset)
            && TryAdd(document, editor, new XYZ(inset.X, inset.Y, inset.Z)))
        {
            outcome.TrialAdded++;
            outcome.Inset++;
            return;
        }

        outcome.Refused++;
    }

    private static bool TryAdd(Document document, SlabShapeEditor editor, XYZ point)
    {
        return Attempt(document, () => editor.AddPoints(new List<XYZ> { point }), $"point at {point.X:0.###}, {point.Y:0.###}");
    }

    /// <summary>Runs the edit in a sub-transaction, which is rolled back (taking any failure Revit posted with it) if Revit refuses it.</summary>
    private static bool Attempt(Document document, Action edit, string what)
    {
        using var subTransaction = new SubTransaction(document);
        subTransaction.Start();
        try
        {
            edit();
            return subTransaction.Commit() == TransactionStatus.Committed;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Revit would not add {what}", what);
            if (subTransaction.HasStarted())
            {
                subTransaction.RollBack();
            }

            return false;
        }
    }

    /// <summary>
    /// Points along a chain of curves at the given spacing, raised by the offset. Arcs get at least a point every 5 degrees, and other curves at least 16 points.
    /// A chain that ends where it starts is closed, and its end point isn't repeated.
    /// </summary>
    internal static List<GradePoint> BuildPath(IReadOnlyList<Curve> curves, double interval, double verticalOffset, out bool closed)
    {
        if (curves is null || curves.Count == 0)
        {
            throw new ArgumentException("There is no line to grade from.", nameof(curves));
        }

        if (!(interval > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The spacing must be greater than zero.");
        }

        var path = new List<GradePoint>();
        for (var c = 0; c < curves.Count; c++)
        {
            var curve = curves[c];
            var segments = Math.Max(1, (int)Math.Ceiling(curve.Length / interval));

            // Curves get extra points so they stay smooth, but never closer together than a fraction of the spacing: an edge picked as
            // many short spline pieces would otherwise get a crowd of points on each piece.
            var finest = Math.Max(1, (int)Math.Floor(curve.Length / Math.Max(interval / 4.0, MinimumPointSpacing)));
            if (curve is Arc arc && arc.Radius > 1e-9)
            {
                segments = Math.Max(segments, Math.Min(finest, (int)Math.Ceiling(curve.Length / arc.Radius / (5.0 * Math.PI / 180.0))));
            }
            else if (curve is not Line)
            {
                // an ellipse or spline has no single radius to size the pieces by
                segments = Math.Max(segments, Math.Min(finest, 16));
            }

            for (var i = c == 0 ? 0 : 1; i <= segments; i++)
            {
                var point = curve.Evaluate((double)i / segments, true);
                path.Add(new GradePoint(point.X, point.Y, point.Z + verticalOffset));
            }
        }

        var first = path[0];
        var last = path[path.Count - 1];
        closed = path.Count > 2 && Math.Abs(first.X - last.X) < 1e-4 && Math.Abs(first.Y - last.Y) < 1e-4;
        if (closed)
        {
            path.RemoveAt(path.Count - 1);
        }

        return path;
    }
}
#endif
