namespace TopoAlign.Alignments;

/// <summary>Ground elevations sampled along the alignment, in feet, plus how many stations missed the surface.</summary>
internal sealed class ProfileSampleResult
{
    public ProfileSampleResult(List<ProfilePoint> samples, int missingCount)
    {
        Samples = samples;
        MissingCount = missingCount;
    }

    public List<ProfilePoint> Samples { get; }
    public int MissingCount { get; }
}
