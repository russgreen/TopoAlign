using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>A Revit category a road can be filed under, with the name shown to the user.</summary>
internal sealed class RoadCategory
{
    public RoadCategory(BuiltInCategory category, string name)
    {
        Category = category;
        Name = name;
    }

    public BuiltInCategory Category { get; }
    public string Name { get; }
}

internal sealed class RoadOptions
{
    /// <summary>Distance between sections along the alignment, in feet.</summary>
    public double Interval { get; set; } = 3.28084;

    /// <summary>Added to the height of every section point, in feet.</summary>
    public double VerticalOffset { get; set; }

    /// <summary>The category the DirectShape is filed under.</summary>
    public BuiltInCategory Category { get; set; } = BuiltInCategory.OST_GenericModel;

    /// <summary>Name of the profile type, kept with the road so it's easy to tell what made it.</summary>
    public string ProfileName { get; set; } = string.Empty;
}

internal sealed class RoadResult
{
    public ElementId RoadId { get; set; } = ElementId.InvalidElementId;
    public int StationCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; the road has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when an earlier road for the same alignment was removed first.</summary>
    public bool ReplacedPrevious { get; set; }
}

/// <summary>Lofts a run of section loops into solids.</summary>
internal static class RoadSolidBuilder
{
    internal sealed class SolidResult
    {
        public List<Solid> Solids { get; } = new();
        public int SkippedSpans { get; set; }
    }

    /// <summary>
    /// Builds solids between consecutive loops. Loops are lofted <paramref name="groupSize"/> at a time (neighbouring groups
    /// share a loop so the surface is continuous); if a group can't be built it falls back to one span at a time, and a span
    /// that still fails is skipped and counted, so one bad section doesn't lose the whole road.
    /// </summary>
    public static SolidResult Build(IReadOnlyList<CurveLoop> loops, int groupSize = 2)
    {
        var result = new SolidResult();
        if (loops is null || loops.Count < 2)
        {
            return result;
        }

        var size = Math.Max(2, groupSize);
        for (var start = 0; start < loops.Count - 1; start += size - 1)
        {
            var count = Math.Min(size, loops.Count - start);
            var group = loops.Skip(start).Take(count).ToList();

            if (count == 2 || !TryLoft(group, result))
            {
                for (var i = 0; i < group.Count - 1; i++)
                {
                    if (!TryLoft(new List<CurveLoop> { group[i], group[i + 1] }, result))
                    {
                        result.SkippedSpans++;
                    }
                }
            }
        }

        return result;
    }

    private static bool TryLoft(IList<CurveLoop> loops, SolidResult result)
    {
        try
        {
            var options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            var solid = GeometryCreationUtilities.CreateLoftGeometry(loops, options);
            if (solid is null || solid.Volume < 1e-9)
            {
                return false;
            }

            result.Solids.Add(solid);
            return true;
        }
        catch (Exception)
        {
            // Revit throws for self-intersecting or degenerate lofts, for example on a radius tighter than the road is wide
            return false;
        }
    }
}

/// <summary>Turns a section and a path into the section's loop standing at a point of the path.</summary>
internal static class SectionLoopFactory
{
    /// <summary>The section as a closed loop of Revit curves at <paramref name="centre"/>, across the horizontal direction <paramref name="tangent"/>.</summary>
    public static CurveLoop CreateLoop(SectionProfile profile, XYZ centre, XYZ tangent, double verticalOffset)
    {
        var origin = new PathPoint(centre.X, centre.Y, centre.Z);

        XYZ ToWorld(PlanPoint point)
        {
            var world = SectionFrame.ToWorld(point.X, point.Y, origin, tangent.X, tangent.Y, verticalOffset);
            return new XYZ(world.X, world.Y, world.Z);
        }

        // each vertex is computed once and shared, so neighbouring curves meet exactly
        var vertices = profile.Segments.Select(s => ToWorld(s.StartPoint)).ToList();
        var curves = new List<Curve>(profile.Segments.Count);
        for (var i = 0; i < profile.Segments.Count; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count];
            var segment = profile.Segments[i];

            if (segment is PlanArc arc)
            {
                curves.Add(Arc.Create(start, end, ToWorld(arc.PointAt(arc.Length / 2.0))));
            }
            else
            {
                curves.Add(Line.CreateBound(start, end));
            }
        }

        return CurveLoop.Create(curves);
    }
}

/// <summary>Builds a road solid along an alignment from a cross-section.</summary>
internal static class RoadBuilder
{
    /// <summary>Marks the DirectShapes this add-in makes, so a road can be found again and replaced.</summary>
    public const string ApplicationId = "TopoAlign.Road";

    private static readonly ILogger log = Log.ForContext(typeof(RoadBuilder));

    /// <summary>Largest distance the path may bend away from a straight chord between two sections, in feet (about 6 mm).</summary>
    private const double ChordTolerance = 0.02;

    /// <summary>Categories a road can be filed under, in order of preference, limited to the ones this document accepts for a DirectShape.</summary>
    public static List<RoadCategory> GetCategories(Document document)
    {
        var all = new List<RoadCategory>();

        // looked up by name because not every Revit version has every category
        foreach (var (name, label) in new[] { ("OST_Roads", "Roads"), ("OST_Hardscape", "Hardscape") })
        {
            if (Enum.TryParse<BuiltInCategory>(name, out var category) && Enum.IsDefined(typeof(BuiltInCategory), category))
            {
                all.Add(new RoadCategory(category, label));
            }
        }

        var generic = new RoadCategory(BuiltInCategory.OST_GenericModel, "Generic Models");
        all.Add(generic);

        var valid = all.Where(c => IsValidCategory(document, c.Category)).ToList();
        return valid.Count > 0 ? valid : new List<RoadCategory> { generic };
    }

    private static bool IsValidCategory(Document document, BuiltInCategory category)
    {
        try
        {
            return DirectShape.IsValidCategoryId(new ElementId(category), document);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The loops (one per section) along an alignment, ready to be lofted.</summary>
    internal static List<CurveLoop> CreateLoops(AlignmentApi.PathReader path, SectionProfile profile, RoadOptions options, out int stationCount)
    {
        var interval = Math.Max(options.Interval, 0.1);
        var planned = StationPlanner.Plan(path.StartStation, path.EndStation, interval, path.HorizontalCurveStations());
        var stations = StationPlanner.Refine(planned,
            station =>
            {
                var point = path.PointAt(station);
                return new PathPoint(point.X, point.Y, point.Z);
            },
            ChordTolerance,
            Math.Max(interval / 16.0, 0.25));

        stationCount = stations.Count;
        var loops = new List<CurveLoop>(stations.Count);
        foreach (var station in stations)
        {
            loops.Add(SectionLoopFactory.CreateLoop(profile, path.PointAt(station), path.HorizontalTangentAt(station), options.VerticalOffset));
        }

        return loops;
    }

    /// <summary>
    /// Builds the road and puts it in the model, replacing an earlier road for the same alignment. Must be called from within
    /// the Revit API context; it opens its own transaction.
    /// </summary>
    public static RoadResult Create(Document document, Element alignment, SectionProfile profile, RoadOptions options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (alignment is null)
        {
            throw new ArgumentNullException(nameof(alignment));
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        options ??= new RoadOptions();

        var path = new AlignmentApi.PathReader(alignment);
        var loops = CreateLoops(path, profile, options, out var stationCount);
        var solids = RoadSolidBuilder.Build(loops);
        if (solids.Solids.Count == 0)
        {
            throw new InvalidOperationException(
                "Revit could not build any geometry for the road. The profile may be too wide for the tightest curve in the alignment, or may overlap itself.");
        }

        var categoryId = IsValidCategory(document, options.Category)
            ? new ElementId(options.Category)
            : new ElementId(BuiltInCategory.OST_GenericModel);

        var result = new RoadResult { StationCount = stationCount, SolidCount = solids.Solids.Count, SkippedSpans = solids.SkippedSpans };

        using var transaction = new Transaction(document, "Create road");
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        var previous = FindRoads(document, alignment).Select(shape => shape.Id).ToList();
        if (previous.Count > 0)
        {
            document.Delete(previous);
            result.ReplacedPrevious = true;
        }

        var shape = DirectShape.CreateElement(document, categoryId);
        shape.ApplicationId = ApplicationId;
        shape.ApplicationDataId = alignment.UniqueId;
        shape.SetShape(solids.Solids.Cast<GeometryObject>().ToList());

        var comments = shape.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
        if (comments is not null && !comments.IsReadOnly)
        {
            var alignmentName = AlignmentApi.GetInfo(alignment)?.Name;
            comments.Set(string.IsNullOrWhiteSpace(options.ProfileName)
                ? $"Road along {alignmentName}"
                : $"Road along {alignmentName} ({options.ProfileName})");
        }

        transaction.Commit();
        result.RoadId = shape.Id;

        if (result.SkippedSpans > 0)
        {
            log.Warning("{skipped} spans of the road along {alignment} could not be built", result.SkippedSpans, alignment.UniqueId);
        }

        return result;
    }

    /// <summary>The roads this add-in has made for an alignment.</summary>
    public static List<DirectShape> FindRoads(Document document, Element alignment)
    {
        return new FilteredElementCollector(document)
            .OfClass(typeof(DirectShape))
            .Cast<DirectShape>()
            .Where(shape => shape.ApplicationId == ApplicationId && shape.ApplicationDataId == alignment.UniqueId)
            .ToList();
    }
}
