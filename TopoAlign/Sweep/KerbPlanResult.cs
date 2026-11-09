namespace TopoAlign.Sweep;

internal sealed class KerbPlanResult
{
    /// <summary>Runs of sections to loft. A run is split where the path turns too sharply to mitre.</summary>
    public List<List<KerbSection>> Runs { get; } = new();

    /// <summary>Why the path can't be swept, or null.</summary>
    public string? Error { get; set; }
}
