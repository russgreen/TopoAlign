using TopoAlign.Alignments;

namespace TopoAlign.ViewModels;

/// <summary>The draped profile for the current chain: simplified PVIs plus the vertical curve length to apply.</summary>
internal sealed class DrapedProfile
{
    public DrapedProfile(List<ProfilePoint> pvis, int missingCount, double minVerticalCurveLength)
    {
        Pvis = pvis;
        MissingCount = missingCount;
        MinVerticalCurveLength = minVerticalCurveLength;
    }

    public List<ProfilePoint> Pvis { get; }
    public int MissingCount { get; }
    public double MinVerticalCurveLength { get; }
}
