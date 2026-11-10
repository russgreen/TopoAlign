using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Sweep.Profiles;

/// <summary>Finds profile families in a project and reads the shape drawn in them.</summary>
internal static class SectionProfileReader
{
    private static readonly ILogger log = Log.ForContext(typeof(SectionProfileReader));

    /// <summary>The profile family types loaded in the document, by name.</summary>
    public static List<ProfileTypeInfo> ListProfileTypes(Document document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return new FilteredElementCollector(document)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_ProfileFamilies)
            .Cast<FamilySymbol>()
            .Select(symbol => new ProfileTypeInfo(symbol.Id, $"{symbol.FamilyName} : {symbol.Name}"))
            .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reads the closed shape drawn in a profile family type: the family is opened in the family editor (and closed again
    /// without saving), the type selected, and its lines and arcs chained into one loop. In the family, X runs across the
    /// road and Y runs up, from the family's origin.
    /// </summary>
    /// <param name="error">Why the shape couldn't be read, ready to show to the user, or null on success.</param>
    public static SectionProfile? Read(Document document, ElementId symbolId, out string? error)
    {
        error = null;

        if (document?.GetElement(symbolId) is not FamilySymbol symbol)
        {
            error = "That profile type could not be found in the project.";
            return null;
        }

        var family = symbol.Family;
        if (family is null || !family.IsEditable)
        {
            error = $"The profile family '{symbol.FamilyName}' can't be opened for reading.";
            return null;
        }

        Document? familyDocument = null;
        try
        {
            familyDocument = document.EditFamily(family);
            SelectType(familyDocument, symbol.Name);

            var curves = new List<Curve>();
            foreach (var element in new FilteredElementCollector(familyDocument).OfClass(typeof(CurveElement)).Cast<CurveElement>())
            {
                // only the lines drawn as the shape: not reference lines, or the sketches behind other geometry
                var drawn = element.CurveElementType == CurveElementType.ModelCurve || element.CurveElementType == CurveElementType.DetailCurve;
                if (!drawn || (element is ModelCurve modelCurve && modelCurve.IsReferenceLine) || element.GeometryCurve is null)
                {
                    continue;
                }

                curves.Add(element.GeometryCurve);
            }

            return FromCurves(curves, symbol.FamilyName, out error);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not read profile family {family}", symbol.FamilyName);
            error = $"The profile family '{symbol.FamilyName}' could not be read: {ex.Message}";
            return null;
        }
        finally
        {
            try
            {
                familyDocument?.Close(false);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not close the profile family document");
            }
        }
    }

    /// <summary>Chains lines and arcs into a closed section. Z is ignored: the profile is read as drawn in plan.</summary>
    internal static SectionProfile? FromCurves(IReadOnlyList<Curve> curves, string name, out string? error)
    {
        error = null;

        if (curves.Count == 0)
        {
            error = $"No lines were found in the profile family '{name}'.";
            return null;
        }

        var chain = CurveChainer.TryChain(curves);
        if (!chain.IsContinuous)
        {
            error = chain.ErrorMessage switch
            {
                "Only line and arc curves are supported." => $"The profile family '{name}' contains curves other than lines and arcs, such as splines.",
                "Selected curves branch at one or more points." => $"The lines in the profile family '{name}' branch; the profile must be a single closed shape.",
                _ => $"The lines in the profile family '{name}' are not connected into one shape."
            };
            return null;
        }

        if (!SectionProfile.TryCreate(PlanSegmentAdapter.FromCurves(chain.Curves), out var profile, out var reason))
        {
            error = $"The profile family '{name}': {reason}";
            return null;
        }

        return profile;
    }

    private static void SelectType(Document familyDocument, string typeName)
    {
        var manager = familyDocument.FamilyManager;
        var type = manager.Types.Cast<FamilyType>().FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
        if (type is null || manager.CurrentType?.Name == typeName)
        {
            return;
        }

        using var transaction = new Transaction(familyDocument, "Select type");
        transaction.Start();
        manager.CurrentType = type;
        transaction.Commit();
    }
}