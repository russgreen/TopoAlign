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

    /// <summary>Segmented lofts (default) or smooth surfaces without seams between sections.</summary>
    public RoadGeometryMethod GeometryMethod { get; set; } = RoadGeometryMethod.SegmentedLoft;

    /// <summary>A DirectShape (default) or a loadable family with an instance placed in the model.</summary>
    public RoadOutput Output { get; set; } = RoadOutput.DirectShape;
}

internal sealed class RoadResult
{
    /// <summary>The DirectShape, or the family instance when the road was made as a family.</summary>
    public ElementId RoadId { get; set; } = ElementId.InvalidElementId;

    /// <summary>The family, when the road was made as one.</summary>
    public ElementId FamilyId { get; set; } = ElementId.InvalidElementId;

    /// <summary>Something the user should know, for example that the smooth method fell back to segments.</summary>
    public string Notice { get; set; } = string.Empty;

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

    /// <summary>The stations where sections stand: evenly spaced, at the joins between curves, and extra ones wherever the path bends.</summary>
    internal static List<double> PlanStations(AlignmentApi.PathReader path, RoadOptions options)
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
    internal static List<CurveLoop> CreateLoops(AlignmentApi.PathReader path, SectionProfile profile, RoadOptions options, out int stationCount)
    {
        return CreateLoops(path, PlanStations(path, options), profile, options, out stationCount);
    }

    private static List<CurveLoop> CreateLoops(AlignmentApi.PathReader path, List<double> stations, SectionProfile profile, RoadOptions options, out int stationCount)
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
        var stations = PlanStations(path, options);

        var notice = string.Empty;
        var solids = new RoadSolidBuilder.SolidResult();
        var smooth = false;
        if (options.GeometryMethod == RoadGeometryMethod.SmoothSurfaces)
        {
            if (SmoothRoadBuilder.TryBuild(path, stations, profile, options, out var smoothSolids, out var problem))
            {
                solids.Solids.AddRange(smoothSolids);
                smooth = true;
            }
            else
            {
                log.Warning("Smooth road along {alignment} fell back to segments: {problem}", alignment.UniqueId, problem);
                notice = $"The smooth surfaces could not be made ({problem}), so the road was made from segments instead.";
            }
        }

        if (!smooth)
        {
            solids = RoadSolidBuilder.Build(CreateLoops(path, stations, profile, options, out _));
        }

        if (solids.Solids.Count == 0)
        {
            throw new InvalidOperationException(
                "Revit could not build any geometry for the road. The profile may be too wide for the tightest curve in the alignment, or may overlap itself.");
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
                var shape = DirectShape.CreateElement(document, categoryId);
                shape.ApplicationId = ApplicationId;
                shape.ApplicationDataId = alignment.UniqueId;
                shape.SetShape(solids.Solids.Cast<GeometryObject>().ToList());
                road = shape;
            }

            var comments = road.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (comments is not null && !comments.IsReadOnly)
            {
                comments.Set(description);
            }

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
    private static List<ElementId> FindPreviousIds(Document document, Element alignment, string? alignmentName)
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
