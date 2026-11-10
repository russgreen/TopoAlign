namespace TopoAlign.Alignments.Profile;

/// <summary>
/// A point of vertical intersection (PVI) of an alignment profile: a distance along the alignment, an elevation and the length
/// of the parabolic vertical curve rounding it (0 for a sharp change of grade). Distances and elevations are in feet.
/// </summary>
internal readonly struct ProfileVertex
{
    public ProfileVertex(double distance, double elevation, double curveLength)
    {
        Distance = distance;
        Elevation = elevation;
        CurveLength = curveLength;
    }

    public double Distance { get; }
    public double Elevation { get; }
    public double CurveLength { get; }

    public ProfileVertex WithDistance(double distance) => new ProfileVertex(distance, Elevation, CurveLength);
    public ProfileVertex WithElevation(double elevation) => new ProfileVertex(Distance, elevation, CurveLength);
    public ProfileVertex WithCurveLength(double curveLength) => new ProfileVertex(Distance, Elevation, curveLength);
}
