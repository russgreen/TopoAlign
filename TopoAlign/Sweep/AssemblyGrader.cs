using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Grading;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
#endif

namespace TopoAlign.Sweep;

/// <summary>Which side of the alignment an assembly is graded on.</summary>
internal enum AssemblySide
{
    /// <summary>To the left of the direction of travel (negative lateral offsets).</summary>
    Left,

    /// <summary>To the right of the direction of travel (positive lateral offsets).</summary>
    Right
}

/// <summary>What grading the ground out from one side of an assembly did.</summary>
internal sealed class SideGradeOutcome
{
    public SideGradeOutcome(AssemblySide side)
    {
        Side = side;
    }

    public AssemblySide Side { get; }

    /// <summary>The toposolid that was graded, or empty when the side met none of them.</summary>
    public string ToposolidName { get; set; } = string.Empty;

    /// <summary>Points along the edge where the ground is above the edge, so the slope climbs.</summary>
    public int Cut { get; set; }

    /// <summary>Points along the edge where the ground is below it, so the slope falls.</summary>
    public int Fill { get; set; }

    /// <summary>Points whose slope did not meet the ground.</summary>
    public int Missed { get; set; }

    /// <summary>Of the missed points, those where the road's edge is not on the toposolid at all.</summary>
    public int MissedOffSurface { get; set; }

    /// <summary>Of the missed points, those where no ground was met within the maximum width.</summary>
    public int MissedTooWide { get; set; }

    /// <summary>Points whose slope ran off the edge of the toposolid before meeting the ground.</summary>
    public int Clipped { get; set; }

    /// <summary>Corner points on the toposolid's edge that could not be set to the graded height by moving them (points were put beside them instead).</summary>
    public int CornersNotMoved { get; set; }

    /// <summary>Edge points Revit refused outright, so the edge there keeps its old height.</summary>
    public int EdgePointsRefused { get; set; }

    /// <summary>0 when the edit went in as planned; 1 or 2 when Revit would not build the shape and a simpler edit was made (see <see cref="ToposolidGrader"/>).</summary>
    public int FallbackLevel { get; set; }

    /// <summary>Points that stand in for the toposolid's edge but were put a little way inside it, because Revit would not take them on it.</summary>
    public int EdgeStepsIn { get; set; }

    /// <summary>Why the side could not be graded, or empty if it was.</summary>
    public string Error { get; set; } = string.Empty;

    public bool Succeeded => string.IsNullOrEmpty(Error);

    public string SideName => Side == AssemblySide.Left ? "Left" : "Right";
}

/// <summary>
/// Grades the existing ground out from the outer edges of an assembly. The edge path is worked out here from the assembly's
/// geometry (no Revit types); <see cref="Apply"/> hands it to the same grader as Grade to Surface.
/// </summary>
internal static class AssemblyGrader
{
    private static readonly ILogger log = Log.ForContext(typeof(AssemblyGrader));

    /// <summary>Outline points this close (feet, across the road) to the outermost one count as being on the outer edge.</summary>
    private const double EdgeTolerance = 0.005;

    /// <summary>How far past the edge (feet) the point that says which way to grade is put.</summary>
    private const double SideDistance = 1.0;

    /// <summary>
    /// Where the slope leaves the assembly on one side: the outermost point across the road of all the placed parts, and at that
    /// distance the highest point. False if there are no parts.
    /// </summary>
    /// <param name="placedParts">The section of each built part as it stands in the assembly (<see cref="AssemblyPart.Place"/>).</param>
    public static bool TryFindShoulder(IEnumerable<SectionProfile> placedParts, AssemblySide side, out double u, out double v)
    {
        u = 0.0;
        v = 0.0;
        var any = false;
        var outlines = new List<List<PlanPoint>>();

        foreach (var part in placedParts)
        {
            var outline = part.Outline();
            outlines.Add(outline);
            foreach (var point in outline)
            {
                if (!any || (side == AssemblySide.Left ? point.X < u : point.X > u))
                {
                    u = point.X;
                    any = true;
                }
            }
        }

        if (!any)
        {
            return false;
        }

        var found = false;
        foreach (var point in outlines.SelectMany(outline => outline))
        {
            if (Math.Abs(point.X - u) <= EdgeTolerance && (!found || point.Y > v))
            {
                v = point.Y;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// The edge of the assembly at each station, in world coordinates, and a point beyond it on the side to grade towards.
    /// </summary>
    /// <param name="pointAt">The alignment's point at a station.</param>
    /// <param name="tangentAt">The horizontal direction of the alignment at a station; its length does not matter.</param>
    /// <param name="verticalOffset">Added to every height, as for the parts themselves (feet).</param>
    public static List<GradePoint> BuildEdgePath(
        IReadOnlyList<double> stations,
        Func<double, PathPoint> pointAt,
        Func<double, (double X, double Y)> tangentAt,
        double u,
        double v,
        double verticalOffset,
        AssemblySide side,
        out double sideX,
        out double sideY)
    {
        if (stations is null || stations.Count < 2)
        {
            throw new ArgumentException("At least two stations are needed to grade from.", nameof(stations));
        }

        var path = new List<GradePoint>(stations.Count);
        foreach (var station in stations)
        {
            var tangent = tangentAt(station);
            var world = SectionFrame.ToWorld(u, v, pointAt(station), tangent.X, tangent.Y, verticalOffset);
            path.Add(new GradePoint(world.X, world.Y, world.Z));
        }

        var first = stations[0];
        var firstTangent = tangentAt(first);
        var beyond = SectionFrame.ToWorld(u + (side == AssemblySide.Left ? -SideDistance : SideDistance), v, pointAt(first), firstTangent.X, firstTangent.Y, verticalOffset);
        sideX = beyond.X;
        sideY = beyond.Y;
        return path;
    }

#if REVIT2024_OR_GREATER
    /// <summary>A toposolid as the summary names it: its type name says little when several share a type, so the id is included.</summary>
    private static string LabelFor(Toposolid toposolid)
    {
        try
        {
            return $"{toposolid.Name} [{toposolid.Id}]";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"[{toposolid.Id}]";
        }
    }

    /// <summary>
    /// Grades the toposolids out from each side of the assembly that has grading turned on. Each side is tried against every toposolid
    /// and changes the ones its slope reaches, so different toposolids can lie either side of the road, or one side can cross several.
    /// A side that fails is reported, not thrown, so the other side is still graded. Must be called from within the Revit API
    /// context; each side on each toposolid opens its own transaction.
    /// </summary>
    /// <param name="placedParts">The built parts as they stand in the assembly.</param>
    public static List<SideGradeOutcome> Apply(
        Document document,
        IReadOnlyList<Toposolid> toposolids,
        SweepAssembly assembly,
        IReadOnlyList<SectionProfile> placedParts,
        AlignmentApi.PathReader path,
        IReadOnlyList<double> stations,
        RoadOptions options)
    {
        var outcomes = new List<SideGradeOutcome>();

        // Remember each toposolid by id and name now: grading one can change the model so that another picked element is replaced or removed,
        // and an element that has gone can't be asked for its name. Each is looked up again just before it is graded.
        var targets = toposolids.Select(toposolid => (Id: toposolid.Id, Label: LabelFor(toposolid))).ToList();

        foreach (var (side, grading) in new[] { (AssemblySide.Left, assembly.Left), (AssemblySide.Right, assembly.Right) })
        {
            if (grading is null || !grading.Enabled)
            {
                continue;
            }

            var sideName = side == AssemblySide.Left ? "Left" : "Right";
            if (!TryFindShoulder(placedParts, side, out var u, out var v))
            {
                outcomes.Add(new SideGradeOutcome(side) { Error = "The assembly has no geometry to grade from." });
                continue;
            }

            List<GradePoint> edge;
            double sideX, sideY, cut, fill;
            try
            {
                edge = BuildEdgePath(
                    stations,
                    station =>
                    {
                        var point = path.PointAt(station);
                        return new PathPoint(point.X, point.Y, point.Z);
                    },
                    station =>
                    {
                        var tangent = path.HorizontalTangentAt(station);
                        return (tangent.X, tangent.Y);
                    },
                    u,
                    v,
                    options.VerticalOffset,
                    side,
                    out sideX,
                    out sideY);
                cut = grading.CutRiseOverRun();
                fill = grading.FillRiseOverRun();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log.Warning(ex, "Grading the {side} of assembly {assembly} failed", side, assembly.Name);
                outcomes.Add(new SideGradeOutcome(side) { Error = ex.Message });
                continue;
            }

            // every toposolid is tried; one the slope never reaches is left alone and is not a failure
            var graded = 0;
            var failed = 0;
            string? missMessage = null;
            foreach (var (id, label) in targets)
            {
                var outcome = new SideGradeOutcome(side) { ToposolidName = label };
                try
                {
                    if (document.GetElement(id) is not Toposolid toposolid || !toposolid.IsValidObject)
                    {
                        outcome.Error = "The toposolid is no longer in the model.";
                        outcomes.Add(outcome);
                        failed++;
                        continue;
                    }

                    var result = ToposolidGrader.ApplyToPath(
                        document,
                        toposolid,
                        edge,
                        false,
                        sideX,
                        sideY,
                        cut,
                        fill,
                        grading.MaxWidthFeet,
                        true,
                        $"Grade topo ({sideName.ToLowerInvariant()} of road assembly)");

                    outcome.Cut = result.Cut;
                    outcome.Fill = result.Fill;
                    outcome.Missed = result.Missed;
                    outcome.MissedOffSurface = result.MissedOffSurface;
                    outcome.MissedTooWide = result.MissedTooWide;
                    outcome.Clipped = result.Clipped;
                    outcome.CornersNotMoved = result.MovesFailed;
                    outcome.EdgePointsRefused = result.PointsRefused;
                    outcome.FallbackLevel = result.FallbackLevel;
                    outcome.EdgeStepsIn = result.PointsInset;
                    outcomes.Add(outcome);
                    graded++;
                }
                catch (SlopeMissesToposolidException ex)
                {
                    missMessage = ex.Message;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    log.Warning(ex, "Grading the {side} of assembly {assembly} on toposolid {toposolid} failed", side, assembly.Name, id);
                    outcome.Error = ex.Message;
                    outcomes.Add(outcome);
                    failed++;
                }
            }

            if (graded == 0 && failed == 0)
            {
                outcomes.Add(new SideGradeOutcome(side)
                {
                    Error = toposolids.Count == 1
                        ? missMessage ?? "The slope does not meet the toposolid."
                        : $"The slope does not meet any of the {toposolids.Count} toposolids picked. Check that one lies on this side of the road."
                });
            }
        }

        return outcomes;
    }
#endif
}
