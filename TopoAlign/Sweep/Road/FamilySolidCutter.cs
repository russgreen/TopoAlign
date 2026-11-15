using Autodesk.Revit.DB;

namespace TopoAlign.Sweep.Road;

/// <summary>Cuts the geometry of a free-form solid in a family document directly, by rebuilding it from the cut pieces.</summary>
internal static class FamilySolidCutter
{
    // a tool face that lies exactly on a solid's edge or face can fail, so the tool is nudged a little and tried again
    private static readonly XYZ[] Nudges =
    {
        new(0, 0, 0), new(0.0007, 0.0011, 0), new(-0.0013, 0.0005, 0), new(0.0021, -0.0017, 0)
    };

    /// <summary>
    /// Takes the tools away from the form's solids and rebuilds the form from what is left. Returns true if the form was rebuilt;
    /// false if it held no solid. Call inside a transaction in the family document.
    /// </summary>
    public static bool Cut(Document familyDocument, FreeFormElement form, Solid tool)
    {
        return Cut(familyDocument, form, new[] { tool });
    }

    /// <inheritdoc cref="Cut(Document, FreeFormElement, Solid)"/>
    public static bool Cut(Document familyDocument, FreeFormElement form, IReadOnlyList<Solid> tools)
    {
        var solids = form.get_Geometry(new Options())?.OfType<Solid>().Where(s => s.Volume > 1e-9).ToList() ?? new List<Solid>();
        if (solids.Count == 0)
        {
            return false;
        }

        var pieces = new List<Solid>();
        foreach (var solid in solids)
        {
            var current = solid;
            foreach (var tool in tools)
            {
                if (current is null)
                {
                    break;
                }

                current = Difference(current, tool);
            }

            if (current is { Volume: > 1e-9 })
            {
                pieces.Add(current);
            }
        }

        familyDocument.Delete(form.Id);
        foreach (var piece in pieces)
        {
            FreeFormElement.Create(familyDocument, piece);
        }

        return true;
    }

    private static Solid? Difference(Solid solid, Solid tool)
    {
        Exception? failure = null;
        foreach (var nudge in Nudges)
        {
            try
            {
                var moved = nudge.IsZeroLength() ? tool : SolidUtils.CreateTransformed(tool, Transform.CreateTranslation(nudge));
                return BooleanOperationsUtils.ExecuteBooleanOperation(solid, moved, BooleanOperationsType.Difference);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }

        throw failure!;
    }
}
