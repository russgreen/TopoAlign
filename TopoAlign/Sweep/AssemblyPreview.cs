using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>
/// Works out the picture of an assembly. It places each profile with <see cref="AssemblyPart.Place"/>, exactly as the
/// builder does, so the preview can't disagree with what is made. Pure geometry with no Revit or WPF types.
/// </summary>
internal static class AssemblyPreview
{
    /// <param name="left">Grading for the left side; its slopes are added to the picture when it is turned on.</param>
    /// <param name="right">Grading for the right side.</param>
    public static AssemblyPreviewResult Build(IReadOnlyList<AssemblyPart> parts, Func<string, SectionProfile?> profileFor,
        SideGrading? left = null, SideGrading? right = null)
    {
        var result = new AssemblyPreviewResult();
        var placedParts = new List<SectionProfile>();
        var first = true;

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var name = AssemblyBuilder.DisplayName(part, i + 1);

            var profile = string.IsNullOrWhiteSpace(part.ProfileName) ? null : profileFor(part.ProfileName);
            if (profile is null)
            {
                result.Missing.Add(name);
                continue;
            }

            var placed = part.Place(profile);
            placedParts.Add(placed);
            var outline = placed.Outline();
            result.Outlines.Add(new PreviewOutline(i, name, outline));

            foreach (var point in outline)
            {
                if (first)
                {
                    result.MinU = result.MaxU = point.X;
                    result.MinV = result.MaxV = point.Y;
                    first = false;
                    continue;
                }

                result.MinU = Math.Min(result.MinU, point.X);
                result.MaxU = Math.Max(result.MaxU, point.X);
                result.MinV = Math.Min(result.MinV, point.Y);
                result.MaxV = Math.Max(result.MaxV, point.Y);
            }
        }

        AddSlopes(result, placedParts, AssemblySide.Left, left);
        AddSlopes(result, placedParts, AssemblySide.Right, right);
        return result;
    }

    /// <summary>Adds the cut and fill lines for a side, from the same shoulder the builder grades from. A slope that can't be used is left out.</summary>
    private static void AddSlopes(AssemblyPreviewResult result, IReadOnlyList<SectionProfile> placedParts, AssemblySide side, SideGrading? grading)
    {
        if (grading is null || !grading.Enabled || !AssemblyGrader.TryFindShoulder(placedParts, side, out var u, out var v))
        {
            return;
        }

        try
        {
            result.Slopes.Add(new PreviewSlope(side, true, u, v, grading.CutRiseOverRun(), $"cut {grading.Describe(grading.CutValue)}"));
            result.Slopes.Add(new PreviewSlope(side, false, u, v, grading.FillRiseOverRun(), $"fill {grading.Describe(grading.FillValue)}"));
        }
        catch (ArgumentOutOfRangeException)
        {
            // a slope of zero (or an angle of 90 degrees) isn't drawn; saving the assembly says why
            result.Slopes.RemoveAll(slope => slope.Side == side);
        }
    }
}