namespace TopoAlign.Alignments.Plan;

/// <summary>
/// Chooses the level of the main road at the kerb returns. The main road alignment's profile gives the level on its centreline;
/// the finished road beside it may be higher or lower (crossfall, camber), so the level can instead come from a picked road element,
/// and a fixed offset can be added on top.
/// </summary>
internal static class TJunctionLevels
{
    /// <summary>
    /// The main road level at a plan point: the road element's level if there is one there, otherwise the alignment profile's level,
    /// plus <paramref name="manualOffset"/>.
    /// </summary>
    public static Func<PlanPoint, double> MainLevel(Func<PlanPoint, double> alignmentLevel, Func<PlanPoint, double?>? roadLevel, double manualOffset)
    {
        return p => (roadLevel?.Invoke(p) ?? alignmentLevel(p)) + manualOffset;
    }
}
