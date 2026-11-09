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

/// <summary>
/// A slope running out from the edge of the assembly, drawn as a dashed line: the cut slope climbs and the fill slope falls.
/// It starts at the shoulder, where the ground is graded from, and carries on outwards for as far as the picture goes.
/// </summary>
internal sealed class PreviewSlope
{
    public PreviewSlope(AssemblySide side, bool isCut, double u, double v, double riseOverRun, string label)
    {
        Side = side;
        IsCut = isCut;
        U = u;
        V = v;
        RiseOverRun = riseOverRun;
        Label = label;
    }

    public AssemblySide Side { get; }

    /// <summary>True for the cut slope (climbs), false for the fill slope (falls).</summary>
    public bool IsCut { get; }

    /// <summary>Where the slope starts, across the road and up, in feet.</summary>
    public double U { get; }

    public double V { get; }

    /// <summary>Always positive; the line goes up by this much per foot outwards for a cut and down for a fill.</summary>
    public double RiseOverRun { get; }

    /// <summary>For example "cut 1:2".</summary>
    public string Label { get; }

    /// <summary>+1 outwards to the right, -1 outwards to the left.</summary>
    public double Direction => Side == AssemblySide.Right ? 1.0 : -1.0;

    /// <summary>The height of the slope at a distance across the road.</summary>
    public double HeightAt(double u) => V + (IsCut ? 1.0 : -1.0) * RiseOverRun * (u - U) * Direction;
}

/// <summary>What the assembly editor draws: the outline of every part whose profile is known, and the space they cover.</summary>
internal sealed class AssemblyPreviewResult
{
    public List<PreviewOutline> Outlines { get; } = new();

    /// <summary>The cut and fill slopes of each side that has grading turned on and a shoulder to start from.</summary>
    public List<PreviewSlope> Slopes { get; } = new();

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
