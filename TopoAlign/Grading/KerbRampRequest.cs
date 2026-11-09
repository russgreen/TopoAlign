#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>What the dialog hands back to the command. All lengths are in feet.</summary>
internal sealed class KerbRampRequest
{
    public KerbRampRequest(
        Toposolid kerbToposolid,
        XYZ kerbAnchor,
        Toposolid targetToposolid,
        XYZ targetAnchor,
        XYZ centre,
        double width,
        double rampRatio,
        double flareRatio,
        double maxRampLength,
        double levelOffset)
    {
        KerbToposolid = kerbToposolid;
        KerbAnchor = kerbAnchor;
        TargetToposolid = targetToposolid;
        TargetAnchor = targetAnchor;
        Centre = centre;
        Width = width;
        RampRatio = rampRatio;
        FlareRatio = flareRatio;
        MaxRampLength = maxRampLength;
        LevelOffset = levelOffset;
    }

    /// <summary>The toposolid the ramp is cut into.</summary>
    public Toposolid KerbToposolid { get; }

    /// <summary>A point on the edge picked as the kerb.</summary>
    public XYZ KerbAnchor { get; }

    /// <summary>The toposolid whose edge sets the level the ramp drops to.</summary>
    public Toposolid TargetToposolid { get; }

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
