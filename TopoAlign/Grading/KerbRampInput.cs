using TopoAlign.Alignments;

namespace TopoAlign.Grading;

/// <summary>Everything the dropped kerb ramp is worked out from. All lengths are in feet.</summary>
internal sealed class KerbRampInput
{
    public KerbRampInput(
        IReadOnlyList<EdgeSegment> kerbOutline,
        double kerbAnchorX,
        double kerbAnchorY,
        IReadOnlyList<EdgeSegment> targetOutline,
        double targetAnchorX,
        double targetAnchorY,
        TriangleSurface surface,
        double centreX,
        double centreY,
        double width,
        double rampRatio,
        double flareRatio,
        double maxRampLength,
        double levelOffset,
        double spacing = 1.0)
    {
        KerbOutline = kerbOutline;
        KerbAnchorX = kerbAnchorX;
        KerbAnchorY = kerbAnchorY;
        TargetOutline = targetOutline;
        TargetAnchorX = targetAnchorX;
        TargetAnchorY = targetAnchorY;
        Surface = surface;
        CentreX = centreX;
        CentreY = centreY;
        Width = width;
        RampRatio = rampRatio;
        FlareRatio = flareRatio;
        MaxRampLength = maxRampLength;
        LevelOffset = levelOffset;
        Spacing = spacing;
    }

    /// <summary>Outline of the toposolid the ramp is cut into (the footway).</summary>
    public IReadOnlyList<EdgeSegment> KerbOutline { get; }

    /// <summary>A point on the picked kerb edge, to say which run of the outline is the kerb.</summary>
    public double KerbAnchorX { get; }

    public double KerbAnchorY { get; }

    /// <summary>Outline of the toposolid whose edge sets the level the ramp drops to (the road).</summary>
    public IReadOnlyList<EdgeSegment> TargetOutline { get; }

    public double TargetAnchorX { get; }
    public double TargetAnchorY { get; }

    /// <summary>The surface of the toposolid being cut.</summary>
    public TriangleSurface Surface { get; }

    /// <summary>The centre of the drop.</summary>
    public double CentreX { get; }

    public double CentreY { get; }

    /// <summary>Width of the dropped section, along the kerb.</summary>
    public double Width { get; }

    /// <summary>The ramp runs 1 up for this much along (12 for 1:12).</summary>
    public double RampRatio { get; }

    /// <summary>The flares rise 1 for this much along the kerb.</summary>
    public double FlareRatio { get; }

    /// <summary>Furthest the ramp may run from the kerb before giving up on meeting the ground.</summary>
    public double MaxRampLength { get; }

    /// <summary>Added to the level taken from the target edge, so the ramp can finish a little above or below it.</summary>
    public double LevelOffset { get; }

    /// <summary>Furthest apart the points along the kerb are.</summary>
    public double Spacing { get; }
}
