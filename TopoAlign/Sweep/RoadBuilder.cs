using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

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

    internal static bool IsValidCategory(Document document, BuiltInCategory category)
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

    /// <summary>The stations where sections stand: evenly spaced, at the joins between curves, and extra ones wherever the path bends.</summary>
    internal static List<double> PlanStations(PathReader path, RoadOptions options)
    {
        var interval = Math.Max(options.Interval, 0.1);
        var planned = StationPlanner.Plan(path.StartStation, path.EndStation, interval, path.HorizontalCurveStations());
        return StationPlanner.Refine(planned,
            station =>
            {
                var point = path.PointAt(station);
                return new PathPoint(point.X, point.Y, point.Z);
            },
            ChordTolerance,
            Math.Max(interval / 16.0, 0.25));
    }

    /// <summary>The loops (one per section) along an alignment, ready to be lofted.</summary>
    internal static List<CurveLoop> CreateLoops(PathReader path, SectionProfile profile, RoadOptions options, out int stationCount)
    {
        return CreateLoops(path, PlanStations(path, options), profile, options, out stationCount);
    }

    private static List<CurveLoop> CreateLoops(PathReader path, List<double> stations, SectionProfile profile, RoadOptions options, out int stationCount)
    {
        stationCount = stations.Count;
        var loops = new List<CurveLoop>(stations.Count);
        foreach (var station in stations)
        {
            loops.Add(SectionLoopFactory.CreateLoop(profile, path.PointAt(station), path.HorizontalTangentAt(station), options.VerticalOffset));
        }

        return loops;
    }

    /// <summary>
    /// Builds the solids of one profile along a path, smooth if asked (falling back to segments, and saying so in the notice)
    /// and otherwise as segmented lofts. Nothing is added to the model.
    /// </summary>
    internal static (SolidResult Solids, string Notice) BuildSolids(
        PathReader path, List<double> stations, SectionProfile profile, RoadOptions options, string alignmentId)
    {
        var notice = string.Empty;
        if (options.GeometryMethod == RoadGeometryMethod.SmoothSurfaces)
        {
            if (SmoothRoadBuilder.TryBuild(path, stations, profile, options, out var smoothSolids, out var problem))
            {
                var smooth = new SolidResult();
                smooth.Solids.AddRange(smoothSolids);
                return (smooth, notice);
            }

            log.Warning("Smooth road along {alignment} fell back to segments: {problem}", alignmentId, problem);
            notice = $"The smooth surfaces could not be made ({problem}), so the road was made from segments instead.";
        }

        return (RoadSolidBuilder.Build(CreateLoops(path, stations, profile, options, out _)), notice);
    }

    /// <summary>Adds a DirectShape of the solids, tagged as this add-in's road for the alignment. Call it inside a transaction.</summary>
    internal static DirectShape CreateShape(Document document, ElementId categoryId, IEnumerable<Solid> solids, Element alignment)
    {
        var shape = DirectShape.CreateElement(document, categoryId);
        shape.ApplicationId = ApplicationId;
        shape.ApplicationDataId = alignment.UniqueId;
        shape.SetShape(solids.Cast<GeometryObject>().ToList());
        return shape;
    }

    internal static void SetComments(Element element, string text)
    {
        var comments = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
        if (comments is not null && !comments.IsReadOnly)
        {
            comments.Set(text);
        }
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

        var path = new PathReader(alignment);
        var stations = PlanStations(path, options);

        var (solids, notice) = BuildSolids(path, stations, profile, options, alignment.UniqueId);

        if (solids.Solids.Count == 0)
        {
            throw new InvalidOperationException(
                "Revit could not build any geometry for the road. The profile may be too wide for the tightest curve in the alignment, or may overlap itself.");
        }

        if (options.Output == RoadOutput.Toposolid)
        {
            return CreateToposolid(document, solids, stations.Count, notice, options);
        }

        var categoryId = IsValidCategory(document, options.Category)
            ? new ElementId(options.Category)
            : new ElementId(BuiltInCategory.OST_GenericModel);

        var result = new RoadResult
        {
            StationCount = stations.Count,
            SolidCount = solids.Solids.Count,
            SkippedSpans = solids.SkippedSpans,
            Notice = notice
        };

        var alignmentName = AlignmentApi.GetInfo(alignment)?.Name;
        var description = string.IsNullOrWhiteSpace(options.ProfileName)
            ? $"Road along {alignmentName}"
            : $"Road along {alignmentName} ({options.ProfileName})";

        // The family file is made before the model is touched, so a failure there leaves the model as it was.
        string? familyFile = null;
        try
        {
            if (options.Output == RoadOutput.Family)
            {
                familyFile = RoadFamilyBuilder.CreateFamilyFile(document, RoadFamilyBuilder.FamilyName(alignment, alignmentName), solids.Solids, categoryId);
            }

            using var transaction = new Transaction(document, "Create road");
            transaction.Start();

            var failureHandlingOptions = transaction.GetFailureHandlingOptions();
            failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
            transaction.SetFailureHandlingOptions(failureHandlingOptions);

            var previous = FindPreviousIds(document, alignment, alignmentName);
            if (previous.Count > 0)
            {
                document.Delete(previous);
                result.ReplacedPrevious = true;
            }

            Element road;
            if (familyFile is not null)
            {
                var placed = RoadFamilyBuilder.Place(document, familyFile, solids.Solids);
                result.FamilyId = placed.Family.Id;
                road = placed.Instance;
            }
            else
            {
                road = CreateShape(document, categoryId, solids.Solids, alignment);
            }

            SetComments(road, description);

            transaction.Commit();
            result.RoadId = road.Id;
        }
        finally
        {
            RoadFamilyBuilder.DeleteFile(familyFile);
        }

        if (result.SkippedSpans > 0)
        {
            log.Warning("{skipped} spans of the road along {alignment} could not be built", result.SkippedSpans, alignment.UniqueId);
        }

        return result;
    }

    /// <summary>
    /// Makes a toposolid from the top of the road. The solids were never added to the model, so nothing of the road is
    /// left behind and an earlier road for the alignment is not touched.
    /// </summary>
    private static RoadResult CreateToposolid(Document document, SolidResult solids, int stationCount, string notice, RoadOptions options)
    {
#if REVIT2024_OR_GREATER
        var made = ToposolidBuilder.Create(document, solids.Solids, options.ToposolidTypeId, options.LevelId);

        var result = new RoadResult
        {
            StationCount = stationCount,
            SolidCount = solids.Solids.Count,
            SkippedSpans = solids.SkippedSpans,
            Notice = string.Join(" ", new[] { notice, made.Notice }.Where(text => !string.IsNullOrEmpty(text))),
            ToposolidId = made.ToposolidId,
            ToposolidPointCount = made.PointCount,
            ToposolidPointsAddedAfterwards = made.PointsAddedAfterwards
        };

        return result;
#else
        throw new InvalidOperationException("Toposolids need Revit 2024 or later.");
#endif
    }

    /// <summary>True for a road this add-in made: a DirectShape it tagged, or an instance of one of its road families.</summary>
    public static bool IsRoad(Element? element)
    {
        return element switch
        {
            DirectShape shape => shape.ApplicationId == ApplicationId,
            FamilyInstance instance => RoadFamilyBuilder.IsRoadFamily(instance.Symbol?.Family),
            _ => false
        };
    }

    /// <summary>The roads this add-in has made for an alignment: DirectShapes and instances of road families.</summary>
    public static List<Element> FindRoads(Document document, Element alignment)
    {
        var roads = new List<Element>();
        roads.AddRange(new FilteredElementCollector(document)
            .OfClass(typeof(DirectShape))
            .Cast<DirectShape>()
            .Where(shape => shape.ApplicationId == ApplicationId && shape.ApplicationDataId == alignment.UniqueId));

        var families = RoadFamilyBuilder.FindFamilies(document, alignment, AlignmentApi.GetInfo(alignment)?.Name);
        if (families.Count > 0)
        {
            var familyIds = new HashSet<long>(families.Select(f => ElementIdValue(f.Id)));
            roads.AddRange(new FilteredElementCollector(document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(instance => instance.Symbol?.Family is { } family && familyIds.Contains(ElementIdValue(family.Id))));
        }

        return roads;
    }

    /// <summary>Everything an earlier road for this alignment left in the model: the shapes, and the families (which take their instances with them).</summary>
    internal static List<ElementId> FindPreviousIds(Document document, Element alignment, string? alignmentName)
    {
        var ids = new List<ElementId>();
        ids.AddRange(new FilteredElementCollector(document)
            .OfClass(typeof(DirectShape))
            .Cast<DirectShape>()
            .Where(shape => shape.ApplicationId == ApplicationId && shape.ApplicationDataId == alignment.UniqueId)
            .Select(shape => shape.Id));
        ids.AddRange(RoadFamilyBuilder.FindFamilies(document, alignment, alignmentName).Select(family => family.Id));
        return ids;
    }

    private static long ElementIdValue(ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }
}