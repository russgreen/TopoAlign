namespace TopoAlign.Sweep;

/// <summary>
/// One cross-section of a kerb: where it stands, which way its (vertical) plane faces and how much its width is stretched
/// so that the kerb keeps its true width through a corner.
/// </summary>
internal readonly struct KerbSection
{
    public KerbSection(PathPoint centre, double dirX, double dirY, double uScale)
    {
        Centre = centre;
        DirX = dirX;
        DirY = dirY;
        UScale = uScale;
    }

    public PathPoint Centre { get; }

    /// <summary>Horizontal direction the section's plane is across (a unit vector; the section's +u is to its right).</summary>
    public double DirX { get; }

    public double DirY { get; }

    /// <summary>Factor applied to the section's u values: 1 on a straight run, 1/cos(half the turn) at a mitred corner.</summary>
    public double UScale { get; }
}
