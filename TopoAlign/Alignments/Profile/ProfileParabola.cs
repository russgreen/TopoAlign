namespace TopoAlign.Alignments.Profile;

/// <summary>A parabolic vertical curve, defined by its tangent points and the point of vertical intersection.</summary>
internal sealed class ProfileParabola
{
    public ProfileParabola(ProfilePoint start, ProfilePoint pvi, ProfilePoint end)
    {
        Start = start;
        Pvi = pvi;
        End = end;
    }

    public ProfilePoint Start { get; }
    public ProfilePoint Pvi { get; }
    public ProfilePoint End { get; }
}
