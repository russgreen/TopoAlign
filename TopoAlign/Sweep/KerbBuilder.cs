using Autodesk.Revit.DB;
using Serilog;

namespace TopoAlign.Sweep;

internal sealed class KerbOptions
{
    /// <summary>Longest distance between sections on a curved edge, in feet.</summary>
    public double Interval { get; set; } = 3.28084;

    /// <summary>Added to the height of every section point, in feet.</summary>
    public double VerticalOffset { get; set; }

    /// <summary>The category of the kerb.</summary>
    public BuiltInCategory Category { get; set; } = BuiltInCategory.OST_GenericModel;

    /// <summary>Name of the profile type, kept with the kerb so it's easy to tell what made it.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>A DirectShape (default) or a loadable family with an instance placed in the model.</summary>
    public RoadOutput Output { get; set; } = RoadOutput.DirectShape;

    /// <summary>
    /// Called with the kerb in the model but not yet committed, so the user can see it. Returns what to do with it.
    /// Null to keep the kerb without asking.
    /// </summary>
    public Func<KerbResult, KerbReviewChoice>? Review { get; set; }
}

/// <summary>What the user decided about a kerb shown for review.</summary>
internal enum KerbReviewChoice
{
    Keep,

    /// <summary>Remove the kerb and build it again on the other side of the edges.</summary>
    Flip,

    Cancel
}

internal sealed class KerbResult
{
    /// <summary>The DirectShape, or the family instance when the kerb was made as a family.</summary>
    public ElementId KerbId { get; set; } = ElementId.InvalidElementId;

    /// <summary>The family, when the kerb was made as one.</summary>
    public ElementId FamilyId { get; set; } = ElementId.InvalidElementId;

    public int RunCount { get; set; }
    public int SectionCount { get; set; }
    public int SolidCount { get; set; }

    /// <summary>Spans between two sections that Revit could not build; the kerb has a gap there.</summary>
    public int SkippedSpans { get; set; }

    /// <summary>True when the profile was flipped to the other side of the edges during review.</summary>
    public bool Flipped { get; set; }

    /// <summary>True when the user cancelled at review; nothing was added to the model.</summary>
    public bool Cancelled { get; set; }

    /// <summary>Something the user should know, for example a run that was left out.</summary>
    public string Notice { get; set; } = string.Empty;
}

/// <summary>Sweeps a profile along picked edges, keeping the profile vertical, and puts the result in the model.</summary>
internal static class KerbBuilder
{
    /// <summary>Marks the DirectShapes this add-in makes for kerbs, so they are never mistaken for roads.</summary>
    public const string ApplicationId = "TopoAlign.Kerb";

    private static readonly ILogger log = Log.ForContext(typeof(KerbBuilder));

    /// <summary>The solids for the runs: a loft between each pair of mitred sections.</summary>
    internal static SolidResult BuildSolids(IReadOnlyList<KerbChain> chains, SectionProfile profile, double verticalOffset, out int runCount, out int sectionCount, out List<string> problems)
    {
        var solids = new SolidResult();
        runCount = 0;
        sectionCount = 0;
        problems = new List<string>();

        foreach (var chain in chains)
        {
            var plan = KerbPlanner.Plan(chain.Points, chain.Closed);
            if (plan.Error is not null)
            {
                problems.Add(plan.Error);
                continue;
            }

            foreach (var run in plan.Runs)
            {
                var loops = new List<CurveLoop>(run.Count);
                try
                {
                    foreach (var section in run)
                    {
                        var centre = new XYZ(section.Centre.X, section.Centre.Y, section.Centre.Z);
                        loops.Add(SectionLoopFactory.CreateLoop(profile, centre, new XYZ(section.DirX, section.DirY, 0.0), verticalOffset, section.UScale));
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "A kerb run could not be set out");
                    problems.Add("Part of the kerb could not be set out; the profile may be too wide for a tight corner.");
                    continue;
                }

                var built = RoadSolidBuilder.Build(loops);
                solids.Solids.AddRange(built.Solids);
                solids.SkippedSpans += built.SkippedSpans;
                runCount++;
                sectionCount += run.Count;
            }
        }

        return solids;
    }

    /// <summary>
    /// Builds the kerb and puts it in the model. Must be called from within the Revit API context; it opens its own
    /// transaction. Throws with a message fit for the user when nothing could be built. When
    /// <see cref="KerbOptions.Review"/> is set the kerb is shown in the model before the transaction is committed, and the
    /// user can keep it, flip it to the other side of the edges, or cancel (nothing is left behind).
    /// </summary>
    public static KerbResult Create(Document document, IReadOnlyList<KerbChain> chains, SectionProfile profile, KerbOptions options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (chains is null || chains.Count == 0)
        {
            throw new InvalidOperationException("No edges or lines were picked.");
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        options ??= new KerbOptions();

        var categoryId = RoadBuilder.IsValidCategory(document, options.Category)
            ? new ElementId(options.Category)
            : new ElementId(BuiltInCategory.OST_GenericModel);

        var flipped = false;
        while (true)
        {
            var result = CreateOnce(document, chains, flipped ? profile.Mirrored() : profile, options, categoryId, out var committed);
            result.Flipped = flipped;
            if (committed)
            {
                return result;
            }

            if (result.Cancelled)
            {
                return result;
            }

            // the user asked to see the other side
            flipped = !flipped;
        }
    }

    private static KerbResult CreateOnce(Document document, IReadOnlyList<KerbChain> chains, SectionProfile profile, KerbOptions options, ElementId categoryId, out bool committed)
    {
        committed = false;

        var solids = BuildSolids(chains, profile, options.VerticalOffset, out var runCount, out var sectionCount, out var problems);
        if (solids.Solids.Count == 0)
        {
            throw new InvalidOperationException(problems.Count > 0
                ? problems[0]
                : "Revit could not build any geometry for the kerb. The profile may be too wide for the tightest curve or corner, or may overlap itself.");
        }

        var result = new KerbResult
        {
            RunCount = runCount,
            SectionCount = sectionCount,
            SolidCount = solids.Solids.Count,
            SkippedSpans = solids.SkippedSpans,
            Notice = string.Join(" ", problems.Distinct())
        };

        var description = string.IsNullOrWhiteSpace(options.ProfileName) ? "Kerb" : $"Kerb ({options.ProfileName})";

        // The family file is made before the model is touched, so a failure there leaves the model as it was.
        string? familyFile = null;
        try
        {
            if (options.Output == RoadOutput.Family)
            {
                familyFile = RoadFamilyBuilder.CreateFamilyFile(document, RoadFamilyBuilder.KerbFamilyName(options.ProfileName), solids.Solids, categoryId);
            }

            // The kerb is committed inside a transaction group, because Revit does not draw the changes of a transaction
            // that is still open. The group lets a kerb that is shown for review be taken back out without a trace.
            using var group = new TransactionGroup(document, "Create kerb");
            group.Start();

            try
            {
                using (var transaction = new Transaction(document, "Create kerb"))
                {
                    transaction.Start();

                    var failureHandlingOptions = transaction.GetFailureHandlingOptions();
                    failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
                    transaction.SetFailureHandlingOptions(failureHandlingOptions);

                    Element kerb;
                    if (familyFile is not null)
                    {
                        var placed = RoadFamilyBuilder.Place(document, familyFile, solids.Solids);
                        result.FamilyId = placed.Family.Id;
                        kerb = placed.Instance;
                    }
                    else
                    {
                        var shape = DirectShape.CreateElement(document, categoryId);
                        shape.ApplicationId = ApplicationId;
                        shape.ApplicationDataId = Guid.NewGuid().ToString();
                        shape.SetShape(solids.Solids.Cast<GeometryObject>().ToList());
                        kerb = shape;
                    }

                    var comments = kerb.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comments is not null && !comments.IsReadOnly)
                    {
                        comments.Set(description);
                    }

                    transaction.Commit();
                    result.KerbId = kerb.Id;
                }

                var choice = options.Review is null ? KerbReviewChoice.Keep : options.Review(result);
                if (choice == KerbReviewChoice.Keep)
                {
                    group.Assimilate();
                    committed = true;
                }
                else
                {
                    // nothing of the trial kerb stays in the model, not even a family loaded for it
                    group.RollBack();
                    result.KerbId = ElementId.InvalidElementId;
                    result.FamilyId = ElementId.InvalidElementId;
                    result.Cancelled = choice == KerbReviewChoice.Cancel;
                }
            }
            catch (Exception)
            {
                if (group.GetStatus() == TransactionStatus.Started)
                {
                    group.RollBack();
                }

                throw;
            }
        }
        finally
        {
            RoadFamilyBuilder.DeleteFile(familyFile);
        }

        if (committed && result.SkippedSpans > 0)
        {
            log.Warning("{skipped} spans of the kerb could not be built", result.SkippedSpans);
        }

        return result;
    }

    /// <summary>True for a kerb this add-in made: a DirectShape it tagged, or an instance of one of its kerb families.</summary>
    public static bool IsKerb(Element? element)
    {
        return element switch
        {
            DirectShape shape => shape.ApplicationId == ApplicationId,
            FamilyInstance instance => RoadFamilyBuilder.IsKerbFamily(instance.Symbol?.Family),
            _ => false
        };
    }
}
