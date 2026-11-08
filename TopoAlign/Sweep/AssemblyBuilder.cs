using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

internal sealed class AssemblyResult
{
    /// <summary>The DirectShapes made, one per part that produced geometry.</summary>
    public List<ElementId> ShapeIds { get; } = new();

    /// <summary>The names of the parts that were built.</summary>
    public List<string> BuiltParts { get; } = new();

    /// <summary>Parts that could not be built, with the reason.</summary>
    public List<string> SkippedParts { get; } = new();

    /// <summary>Something the user should know, for example a part that fell back from smooth surfaces.</summary>
    public string Notice { get; set; } = string.Empty;

    public int StationCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; a part has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when an earlier road for the same alignment was removed first.</summary>
    public bool ReplacedPrevious { get; set; }
}

/// <summary>Sweeps every part of an assembly along an alignment and makes one DirectShape per part.</summary>
internal static class AssemblyBuilder
{
    private static readonly ILogger log = Log.ForContext(typeof(AssemblyBuilder));

    /// <summary>
    /// Builds the assembly and puts it in the model, replacing whatever this add-in made earlier for the same alignment.
    /// Must be called from within the Revit API context; it opens its own transaction.
    /// </summary>
    /// <param name="profiles">The section of each part's profile type, by <see cref="AssemblyPart.ProfileName"/>.</param>
    public static AssemblyResult Create(Document document, Element alignment, SweepAssembly assembly,
        IReadOnlyDictionary<string, SectionProfile> profiles, RoadOptions options)
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

        var path = new AlignmentApi.PathReader(alignment);
        var stations = RoadBuilder.PlanStations(path, options);
        var alignmentName = AlignmentApi.GetInfo(alignment)?.Name;

        var result = new AssemblyResult { StationCount = stations.Count };
        var notices = new List<string>();
        var built = new List<(string Label, AssemblyPart Part, List<Solid> Solids)>();

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

        result.Notice = string.Join(" ", notices);
        if (result.SkippedSpans > 0)
        {
            log.Warning("{skipped} spans of the assembly along {alignment} could not be built", result.SkippedSpans, alignment.UniqueId);
        }

        return result;
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
