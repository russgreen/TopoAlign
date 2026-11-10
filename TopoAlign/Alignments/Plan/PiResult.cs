namespace TopoAlign.Alignments.Plan;

internal sealed class PiResult
{
    public PiResult(List<PlanSegment> segments, List<string> errors)
    {
        Segments = segments;
        Errors = errors;
    }

    public List<PlanSegment> Segments { get; }

    /// <summary>User-readable reasons the PIs can't be laid out; empty when the result is valid.</summary>
    public List<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
