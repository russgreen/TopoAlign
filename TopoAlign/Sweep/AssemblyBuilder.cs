using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>Sweeps every part of an assembly along an alignment and makes one DirectShape per part.</summary>
internal static class AssemblyBuilder
{
    private static readonly ILogger log = Log.ForContext(typeof(AssemblyBuilder));

    /// <summary>
    /// Builds the assembly and puts it in the model, replacing whatever this add-in made earlier for the same alignment.
    /// Must be called from within the Revit API context; it opens its own transaction.
    /// </summary>
    /// <param name="profiles">The section of each part's profile type, by <see cref="AssemblyPart.ProfileName"/>.</param>
    /// <param name="topographies">
    /// The toposolids to grade out from the sides of the assembly, using the assembly's cut and fill slopes. Each side changes
    /// the ones its slope reaches, so different toposolids can lie either side of the road. Null or empty leaves the ground alone.
    /// The road is made first, so a problem grading never loses it.
    /// </param>
    public static AssemblyResult Create(Document document, Element alignment, SweepAssembly assembly,
        IReadOnlyDictionary<string, SectionProfile> profiles, RoadOptions options, IReadOnlyList<Element>? topographies = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (alignment is null)
        {
            throw new ArgumentNullException(nameof(alignment));
        }

        if (assembly is null || assembly.Parts.Count == 0)
        {
            throw new ArgumentException("The assembly has no parts.", nameof(assembly));
        }

        options ??= new RoadOptions();

        var path = new PathReader(alignment);
        var stations = RoadBuilder.PlanStations(path, options);
        var alignmentName = AlignmentApi.GetInfo(alignment)?.Name;

        var result = new AssemblyResult { StationCount = stations.Count };
        var notices = new List<string>();
        var built = new List<(string Label, AssemblyPart Part, List<Solid> Solids)>();
        var placedParts = new List<SectionProfile>();

        // everything is built before the model is touched, so a failure leaves the model as it was
        for (var index = 0; index < assembly.Parts.Count; index++)
        {
            var part = assembly.Parts[index];
            var label = DisplayName(part, index + 1);
            if (!profiles.TryGetValue(part.ProfileName, out var profile))
            {
                result.SkippedParts.Add($"{label}: its profile '{part.ProfileName}' was not found.");
                continue;
            }

            try
            {
                var placed = part.Place(profile);
                var (solids, notice) = RoadBuilder.BuildSolids(path, stations, placed, options, alignment.UniqueId);
                if (!string.IsNullOrEmpty(notice))
                {
                    notices.Add($"{label}: {notice}");
                }

                if (solids.Solids.Count == 0)
                {
                    result.SkippedParts.Add($"{label}: Revit could not build any geometry. The part may be too wide for the tightest curve, or may overlap itself.");
                    continue;
                }

                result.SkippedSpans += solids.SkippedSpans;
                result.SolidCount += solids.Solids.Count;
                built.Add((label, part, solids.Solids));
                placedParts.Add(placed);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                log.Warning(ex, "Part {part} of assembly {assembly} could not be built", label, assembly.Name);
                result.SkippedParts.Add($"{label}: {ex.Message}");
            }
        }

        if (built.Count == 0)
        {
            throw new InvalidOperationException(
                "Revit could not build any geometry for the assembly. " + string.Join(" ", result.SkippedParts));
        }

        using var transaction = new Transaction(document, "Create road assembly");
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        var previous = RoadBuilder.FindPreviousIds(document, alignment, alignmentName);
        if (previous.Count > 0)
        {
            document.Delete(previous);
            result.ReplacedPrevious = true;
        }

        foreach (var (label, part, solids) in built)
        {
            var category = ParseCategory(part.Category);
            var categoryId = RoadBuilder.IsValidCategory(document, category)
                ? new ElementId(category)
                : new ElementId(BuiltInCategory.OST_GenericModel);

            var shape = RoadBuilder.CreateShape(document, categoryId, solids, alignment);
            RoadBuilder.SetComments(shape, $"Road assembly '{assembly.Name}' - {label} along {alignmentName}");

            result.ShapeIds.Add(shape.Id);
            result.BuiltParts.Add(label);
        }

        transaction.Commit();

        if (topographies is { Count: > 0 } && (assembly.Left.Enabled || assembly.Right.Enabled))
        {
            GradeTopography(document, topographies, assembly, placedParts, path, stations, options, result);
        }

        result.Notice = string.Join(" ", notices);
        if (result.SkippedSpans > 0)
        {
            log.Warning("{skipped} spans of the assembly along {alignment} could not be built", result.SkippedSpans, alignment.UniqueId);
        }

        return result;
    }

    private static void GradeTopography(Document document, IReadOnlyList<Element> topographies, SweepAssembly assembly, IReadOnlyList<SectionProfile> placedParts,
        PathReader path, IReadOnlyList<double> stations, RoadOptions options, AssemblyResult result)
    {
#if REVIT2024_OR_GREATER
        var toposolids = topographies.OfType<Toposolid>().Where(t => t.IsValidObject).ToList();
        if (toposolids.Count == 0)
        {
            result.GradingNotice = topographies.Count == 1
                ? "The ground was not graded: the element picked is not a toposolid."
                : "The ground was not graded: none of the elements picked is a toposolid.";
            return;
        }

        if (toposolids.Count < topographies.Count)
        {
            var skipped = topographies.Count - toposolids.Count;
            result.GradingNotice = $"{skipped} of the elements picked {(skipped == 1 ? "was" : "were")} left alone because {(skipped == 1 ? "it is" : "they are")} not {(skipped == 1 ? "a toposolid" : "toposolids")} or {(skipped == 1 ? "is" : "are")} no longer in the model.";
        }

        result.Grading.AddRange(AssemblyGrader.Apply(document, toposolids, assembly, placedParts, path, stations, options));
#else
        result.GradingNotice = "The ground was not graded: toposolids need Revit 2024 or later.";
#endif
    }

    /// <summary>The part's name, or "Part n" when it has none.</summary>
    internal static string DisplayName(AssemblyPart part, int number)
    {
        if (!string.IsNullOrWhiteSpace(part.Name))
        {
            return part.Name.Trim();
        }

        return number > 0 ? $"Part {number}" : "Part";
    }

    /// <summary>The category named in the file, or Generic Models if it is blank or unknown to this version of Revit.</summary>
    internal static BuiltInCategory ParseCategory(string name)
    {
        return Enum.TryParse<BuiltInCategory>(name, out var category) && Enum.IsDefined(typeof(BuiltInCategory), category)
            ? category
            : BuiltInCategory.OST_GenericModel;
    }
}