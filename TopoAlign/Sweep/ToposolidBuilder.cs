#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>What converting a road to a toposolid made.</summary>
internal sealed class ToposolidResult
{
    public ElementId ToposolidId { get; set; } = ElementId.InvalidElementId;
    public int PointCount { get; set; }
    public int LoopCount { get; set; }

    /// <summary>True when Revit would not make the toposolid from its boundary and points in one go, so the points were added afterwards.</summary>
    public bool PointsAddedAfterwards { get; set; }

    public bool DeletedOriginal { get; set; }
    public string Notice { get; set; } = string.Empty;
}

/// <summary>Makes a toposolid whose top surface is the top surface of a road made by Sweep Along Alignment.</summary>
internal static class ToposolidBuilder
{
    private static readonly ILogger log = Log.ForContext(typeof(ToposolidBuilder));

    public static List<ToposolidType> GetTypes(Document document)
    {
        return new FilteredElementCollector(document)
            .OfClass(typeof(ToposolidType))
            .Cast<ToposolidType>()
            .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static List<Level> GetLevels(Document document)
    {
        return new FilteredElementCollector(document)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(level => level.Elevation)
            .ToList();
    }

    /// <summary>The highest level at or below the bottom of the road, or the lowest level if the road is below them all.</summary>
    public static Level? DefaultLevel(IReadOnlyList<Level> levels, Element road)
    {
        if (levels.Count == 0)
        {
            return null;
        }

        var bottom = road.get_BoundingBox(null)?.Min.Z;
        if (bottom is null)
        {
            return levels[0];
        }

        return levels.LastOrDefault(level => level.Elevation <= bottom.Value + 0.01) ?? levels[0];
    }

    /// <summary>Reads the up-facing triangles of the road and works out their outline and points.</summary>
    internal static RoadSurfaceOutline ReadOutline(Element road)
    {
        var builder = new RoadSurfaceOutlineBuilder();
        TopographyReader.ForEachUpFacingTriangle(road, (a, b, c) => builder.AddTriangle(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z));
        return BuildOutline(builder);
    }

    /// <summary>As above, for solids that haven't been added to the model.</summary>
    internal static RoadSurfaceOutline ReadOutline(IEnumerable<Solid> solids)
    {
        var builder = new RoadSurfaceOutlineBuilder();
        TopographyReader.ForEachUpFacingTriangle(solids.Cast<GeometryObject>(), (a, b, c) => builder.AddTriangle(a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z));
        return BuildOutline(builder);
    }

    private static RoadSurfaceOutline BuildOutline(RoadSurfaceOutlineBuilder builder)
    {
        if (builder.TriangleCount == 0)
        {
            throw new InvalidOperationException("No upward-facing surface could be read from the road.");
        }

        var outline = builder.Build();
        if (outline.Loops.Count == 0)
        {
            throw new InvalidOperationException("The outline of the road's surface could not be worked out.");
        }

        return outline;
    }

    /// <summary>
    /// Makes the toposolid in its own transaction, and removes the road if asked. Must be called from within the Revit API context.
    /// </summary>
    public static ToposolidResult Create(Document document, Element road, ElementId typeId, ElementId levelId, bool deleteOriginal)
    {
        if (road is null)
        {
            throw new ArgumentNullException(nameof(road));
        }

        var result = Create(document, ReadOutline(road), typeId, levelId);

        if (deleteOriginal)
        {
            result.DeletedOriginal = DeleteRoad(document, road);
        }

        return result;
    }

    /// <summary>
    /// Makes the toposolid from solids that were built but never added to the model, so the road leaves nothing behind.
    /// Must be called from within the Revit API context.
    /// </summary>
    public static ToposolidResult Create(Document document, IReadOnlyList<Solid> solids, ElementId typeId, ElementId levelId)
    {
        if (solids is null)
        {
            throw new ArgumentNullException(nameof(solids));
        }

        return Create(document, ReadOutline(solids), typeId, levelId);
    }

    private static ToposolidResult Create(Document document, RoadSurfaceOutline outline, ElementId typeId, ElementId levelId)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        // a toposolid boundary is flat; the points give the surface its shape
        var boundaryZ = outline.Points.Min(point => point.Z);
        var loops = outline.Loops.Select(loop => CreateLoop(loop, boundaryZ)).ToList();
        var points = outline.Points.Select(point => new XYZ(point.X, point.Y, point.Z)).ToList();

        var result = new ToposolidResult
        {
            PointCount = points.Count,
            LoopCount = loops.Count
        };

        if (outline.Loops.Count > 1)
        {
            result.Notice = $"The road's surface is in {outline.Loops.Count} separate pieces, so the toposolid has {outline.Loops.Count} boundaries.";
        }

        if (outline.OpenChains > 0)
        {
            result.Notice += (result.Notice.Length > 0 ? " " : string.Empty) +
                             "Part of the road's edge could not be closed into a boundary and was left out.";
        }

        var toposolid = TryCreate(document, "Convert road to toposolid", () => Toposolid.Create(document, loops, points, typeId, levelId));

        if (toposolid is null)
        {
            // Some versions don't accept points on the boundary when the toposolid is made; edit the shape afterwards instead.
            result.PointsAddedAfterwards = true;
            toposolid = TryCreate(document, "Convert road to toposolid", () =>
            {
                var created = Toposolid.Create(document, loops, typeId, levelId);
                created.GetSlabShapeEditor().AddPoints(points);
                return created;
            }, rethrow: true);
        }

        result.ToposolidId = toposolid!.Id;
        return result;
    }

    private static Toposolid? TryCreate(Document document, string name, Func<Toposolid> create, bool rethrow = false)
    {
        using var transaction = new Transaction(document, name);
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        try
        {
            var toposolid = create();
            if (transaction.Commit() != TransactionStatus.Committed)
            {
                throw new InvalidOperationException("Revit would not make the toposolid.");
            }

            return toposolid;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Making a toposolid from a road failed");
            if (transaction.HasStarted())
            {
                transaction.RollBack();
            }

            if (rethrow)
            {
                throw new InvalidOperationException($"Revit could not make a toposolid from this road: {ex.Message}", ex);
            }

            return null;
        }
    }

    private static CurveLoop CreateLoop(IReadOnlyList<SurfaceVertex> corners, double z)
    {
        var loop = new CurveLoop();
        for (var i = 0; i < corners.Count; i++)
        {
            var start = corners[i];
            var end = corners[(i + 1) % corners.Count];
            loop.Append(Line.CreateBound(new XYZ(start.X, start.Y, z), new XYZ(end.X, end.Y, z)));
        }

        return loop;
    }

    /// <summary>Deletes a road in its own transaction; a road made as a family goes with its family, as when a road is replaced.</summary>
    private static bool DeleteRoad(Document document, Element road)
    {
        var id = road is FamilyInstance { Symbol.Family: { } family } ? family.Id : road.Id;

        using var transaction = new Transaction(document, "Delete road");
        transaction.Start();
        document.Delete(id);
        return transaction.Commit() == TransactionStatus.Committed;
    }
}
#endif
