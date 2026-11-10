using TopoAlign.Alignments;
using TopoAlign.Grading.Slope;

namespace TopoAlign.Grading.Kerb;

internal sealed class KerbRampResult
{
    /// <summary>Points to put in the surface.</summary>
    public List<GradePoint> NewPoints { get; } = new();

    /// <summary>Plan outlines of the ramp and flares. Existing surface points inside them are replaced.</summary>
    public List<GradePoint[]> ClearPolygons { get; } = new();

    public int Stations { get; set; }

    /// <summary>Longest distance the ramp runs from the kerb, measured square to it.</summary>
    public double RampLength { get; set; }

    /// <summary>How far along the kerb each flare runs, before and after the ramp. Zero where there is none.</summary>
    public double StartFlareLength { get; set; }

    public double EndFlareLength { get; set; }

    public List<string> Warnings { get; } = new();

    /// <summary>True when (x, y) is on or inside the ramp or a flare, so any existing surface point there should be replaced.</summary>
    public bool Contains(double x, double y, double tolerance)
    {
        foreach (var polygon in ClearPolygons)
        {
            if (GradingResult.QuadContains(polygon, x, y, tolerance))
            {
                return true;
            }
        }

        return false;
    }
}
