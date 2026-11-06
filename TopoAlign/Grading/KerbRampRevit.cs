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

internal sealed class KerbRampApplyResult
{
    public int PointsRemoved { get; set; }
    public int PointsAdded { get; set; }
    public double RampLength { get; set; }
    public double StartFlareLength { get; set; }
    public double EndFlareLength { get; set; }
    public List<string> Warnings { get; } = new();
}

/// <summary>Reads the two toposolids, works out the dropped kerb and rebuilds the footway's points.</summary>
internal static class KerbRampRevit
{
    // a vertex this close (in plan) to the ramp's or a flare's outline counts as part of it
    private const double PlanTolerance = 1.0 / (16.0 * 12.0);

    /// <summary>Must be called from within the Revit API context.</summary>
    public static KerbRampApplyResult Apply(Document document, KerbRampRequest request)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var toposolid = request.KerbToposolid;
        var surface = TopographyReader.Read(toposolid);

        var input = new KerbRampInput(
            TopoEdgeAlignerRevit.ReadOutline(toposolid),
            request.KerbAnchor.X,
            request.KerbAnchor.Y,
            TopoEdgeAlignerRevit.ReadOutline(request.TargetToposolid),
            request.TargetAnchor.X,
            request.TargetAnchor.Y,
            surface,
            request.Centre.X,
            request.Centre.Y,
            request.Width,
            request.RampRatio,
            request.FlareRatio,
            request.MaxRampLength,
            request.LevelOffset);

        var ramp = KerbRamp.Build(input);

        var result = new KerbRampApplyResult
        {
            RampLength = ramp.RampLength,
            StartFlareLength = ramp.StartFlareLength,
            EndFlareLength = ramp.EndFlareLength
        };
        result.Warnings.AddRange(ramp.Warnings);

        // everything in the surface that isn't being replaced goes back in
        var kept = new List<XYZ>();
        foreach (SlabShapeVertex vertex in toposolid.GetSlabShapeEditor().SlabShapeVertices)
        {
            var position = vertex.Position;
            if (ramp.Contains(position.X, position.Y, PlanTolerance))
            {
                result.PointsRemoved++;
            }
            else
            {
                kept.Add(new XYZ(position.X, position.Y, position.Z));
            }
        }

        // the new points come first so they win over an existing point within the duplicate tolerance
        var points = ramp.NewPoints
            .Select(point => new XYZ(point.X, point.Y, point.Z))
            .Concat(kept)
            .Distinct(new XyzEqualityComparer())
            .ToList();
        result.PointsAdded = points.Count - kept.Count;

        ToposolidGrader.ReplacePoints(document, toposolid, points, "Dropped kerb ramp");
        return result;
    }
}
#endif
