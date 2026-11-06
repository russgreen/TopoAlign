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
    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }
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
            Missed = grading.MissCount
        };

        if (grading.Corridor.Count == 0)
        {
            throw new InvalidOperationException(
                "The slope does not meet the toposolid anywhere along the line. " +
                "Check that the side you picked is towards the toposolid, and that the maximum width is long enough.");
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
            else
            {
                kept.Add(new XYZ(position.X, position.Y, position.Z));
            }
        }

        // the new points come first so they win over an existing point within the duplicate tolerance
        var points = grading.NewPoints
            .Select(point => new XYZ(point.X, point.Y, point.Z))
            .Concat(kept)
            .Distinct(new XyzEqualityComparer())
            .ToList();
        result.PointsAdded = points.Count - kept.Count;

        ReplacePoints(document, toposolid, points, "Grade to surface");
        return result;
    }

    /// <summary>
    /// Gives the toposolid exactly these points, in one transaction. Must be called from within the Revit API context.
    /// </summary>
    internal static void ReplacePoints(Document document, Toposolid toposolid, IReadOnlyList<XYZ> points, string transactionName)
    {
        using var transaction = new Transaction(document, transactionName);
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        try
        {
            var editor = toposolid.GetSlabShapeEditor();
            editor.ResetSlabShape();
            editor.Enable();
            document.Regenerate();
            editor.AddPoints(points.ToList());

            if (transaction.Commit() != TransactionStatus.Committed)
            {
                throw new InvalidOperationException("Revit would not accept the new points.");
            }
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
            if (curve is Arc arc && arc.Radius > 1e-9)
            {
                segments = Math.Max(segments, (int)Math.Ceiling(curve.Length / arc.Radius / (5.0 * Math.PI / 180.0)));
            }
            else if (curve is not Line)
            {
                // an ellipse or spline has no single radius to size the pieces by
                segments = Math.Max(segments, 16);
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
