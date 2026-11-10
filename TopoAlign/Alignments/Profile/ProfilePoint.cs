namespace TopoAlign.Alignments.Profile;

/// <summary>A point on an alignment profile: distance along the alignment and elevation, both in feet.</summary>
internal readonly struct ProfilePoint
{
    public ProfilePoint(double station, double elevation)
    {
        Station = station;
        Elevation = elevation;
    }

    public double Station { get; }
    public double Elevation { get; }
}
