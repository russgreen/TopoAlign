namespace TopoAlign.Alignments.Plan;

/// <summary>Where the side road's last straight reaches a main-road straight.</summary>
internal readonly struct TJunctionHit
{
    public TJunctionHit(PlanPoint point, int mainIndex, double distance)
    {
        Point = point;
        MainIndex = mainIndex;
        Distance = distance;
    }

    public PlanPoint Point { get; }
    public int MainIndex { get; }

    /// <summary>How far the side road's end is from the junction.</summary>
    public double Distance { get; }
}
