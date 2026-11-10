namespace TopoAlign.Alignments.Profile;

/// <summary>A straight (constant grade) profile segment between two station/elevation points.</summary>
internal sealed class ProfileLine
{
    public ProfileLine(ProfilePoint start, ProfilePoint end)
    {
        Start = start;
        End = end;
    }

    public ProfilePoint Start { get; }
    public ProfilePoint End { get; }
}
