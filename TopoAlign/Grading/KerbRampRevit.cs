#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

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