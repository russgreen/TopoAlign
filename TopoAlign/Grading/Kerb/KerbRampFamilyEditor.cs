#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Sweep.Road;

namespace TopoAlign.Grading.Kerb;

/// <summary>Cuts a dropped kerb into a road part that is still a family (a road not converted to a toposolid), by editing the family's geometry.</summary>
internal static class KerbRampFamilyEditor
{
    private static readonly ILogger log = Log.ForContext(typeof(KerbRampFamilyEditor));

    private const double Margin = 1.0; // feet the cutting tool reaches above the part
    private const double MinVolume = 1e-9;

    /// <summary>
    /// Cuts the ramp into the family of <paramref name="part"/> and loads the family back. Must be called with no transaction open.
    /// Everything the family editing needs is read from the instance first, as loading a family can leave held elements stale.
    /// </summary>
    public static void Apply(Document document, FamilyInstance part, KerbRampResult ramp, KerbRampApplyResult result)
    {
        var family = part.Symbol?.Family ?? throw new InvalidOperationException("The road part has no family.");
        var familyName = family.Name;
        var toFamily = part.GetTransform().Inverse;
        var box = part.get_BoundingBox(null) ?? throw new InvalidOperationException("The road part has no geometry.");

        var tools = KerbRampTool.Build(ramp, box.Max.Z + Margin, result.Warnings);
        if (tools.Count == 0)
        {
            throw new InvalidOperationException("The ramp could not be turned into a cut.");
        }

        var lowest = ramp.NewPoints.Min(p => p.Z);
        if (lowest < box.Min.Z)
        {
            result.Warnings.Add($"The ramp drops below the bottom of '{familyName}', so the part is cut right through there.");
        }

        if (!family.IsEditable)
        {
            throw new InvalidOperationException($"The family '{familyName}' can't be edited.");
        }

        Document? familyDocument = null;
        try
        {
            familyDocument = document.EditFamily(family);
            var familyTools = tools.Select(t => SolidUtils.CreateTransformed(t, toFamily)).ToList();

            using var transaction = new Transaction(familyDocument, "Dropped kerb ramp");
            transaction.Start();

            var forms = new FilteredElementCollector(familyDocument)
                .OfClass(typeof(FreeFormElement))
                .Cast<FreeFormElement>()
                .Where(f => f.IsSolid)
                .ToList();
            foreach (var form in forms)
            {
                if (Reaches(form, familyTools) && FamilySolidCutter.Cut(familyDocument, form, familyTools))
                {
                    result.SolidsCut++;
                }
            }

            if (result.SolidsCut == 0)
            {
                transaction.RollBack();
                result.Warnings.Add($"The ramp does not reach any solid in '{familyName}', so nothing was changed.");
                return;
            }

            transaction.Commit();
            familyDocument.LoadFamily(document, new FamilyOverwriteOptions());
        }
        finally
        {
            familyDocument?.Close(false);
        }
    }

    /// <summary>True if the form's solid overlaps a tool. If that can't be worked out the form is taken to overlap, so it is cut rather than missed.</summary>
    private static bool Reaches(FreeFormElement form, IReadOnlyList<Solid> tools)
    {
        try
        {
            var solids = form.get_Geometry(new Options())?.OfType<Solid>().Where(s => s.Volume > MinVolume).ToList() ?? new List<Solid>();
            foreach (var solid in solids)
            {
                foreach (var tool in tools)
                {
                    var common = BooleanOperationsUtils.ExecuteBooleanOperation(solid, tool, BooleanOperationsType.Intersect);
                    if (common is { Volume: > 1e-6 })
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            log.Information(ex, "Could not tell whether the ramp reaches a solid");
            return true;
        }
    }
}
#endif
