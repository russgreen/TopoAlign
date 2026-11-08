using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>The outline of one part of an assembly as it stands across the road, in (u, v) feet.</summary>
internal sealed class PreviewOutline
{
    public PreviewOutline(int index, string name, List<PlanPoint> points)
    {
        Index = index;
        Name = name;
        Points = points;
    }

    /// <summary>Position of the part in the assembly, from 0.</summary>
    public int Index { get; }

    public string Name { get; }

    /// <summary>The closed outline, in order; the last point joins back to the first.</summary>
    public List<PlanPoint> Points { get; }
}

/// <summary>What the assembly editor draws: the outline of every part whose profile is known, and the space they cover.</summary>
internal sealed class AssemblyPreviewResult
{
    public List<PreviewOutline> Outlines { get; } = new();

    /// <summary>Parts that could not be drawn because their profile is not chosen or could not be read.</summary>
    public List<string> Missing { get; } = new();

    public bool HasOutlines => Outlines.Count > 0;

    public double MinU { get; set; }
    public double MaxU { get; set; }
    public double MinV { get; set; }
    public double MaxV { get; set; }
}

/// <summary>
/// Works out the picture of an assembly. It places each profile with <see cref="AssemblyPart.Place"/>, exactly as the
/// builder does, so the preview can't disagree with what is made. Pure geometry with no Revit or WPF types.
/// </summary>
internal static class AssemblyPreview
{
    public static AssemblyPreviewResult Build(IReadOnlyList<AssemblyPart> parts, Func<string, SectionProfile?> profileFor)
    {
        var result = new AssemblyPreviewResult();
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

            var outline = part.Place(profile).Outline();
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

        return result;
    }
}
