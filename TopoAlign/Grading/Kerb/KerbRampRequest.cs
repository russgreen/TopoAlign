#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading.Kerb;

/// <summary>What the dialog hands back to the command. All lengths are in feet.</summary>
internal sealed class KerbRampRequest
{
    public KerbRampRequest(
        Element kerbElement,
        XYZ kerbAnchor,
        Element targetElement,
        XYZ targetAnchor,
        XYZ centre,
        double width,
        double rampRatio,
        double flareRatio,
        double maxRampLength,
        double levelOffset)
    {
        KerbElement = kerbElement;
        KerbAnchor = kerbAnchor;
        TargetElement = targetElement;
        TargetAnchor = targetAnchor;
        Centre = centre;
        Width = width;
        RampRatio = rampRatio;
        FlareRatio = flareRatio;
        MaxRampLength = maxRampLength;
        LevelOffset = levelOffset;
    }

    /// <summary>The toposolid or road part the ramp is cut into.</summary>
    public Element KerbElement { get; }

    /// <summary>A point on the edge picked as the kerb.</summary>
    public XYZ KerbAnchor { get; }

    /// <summary>The toposolid or road part whose edge sets the level the ramp drops to.</summary>
    public Element TargetElement { get; }

    /// <summary>A point on the edge picked as the target.</summary>
    public XYZ TargetAnchor { get; }

    public XYZ Centre { get; }
    public double Width { get; }
    public double RampRatio { get; }
    public double FlareRatio { get; }
    public double MaxRampLength { get; }
    public double LevelOffset { get; }
}
#endif
