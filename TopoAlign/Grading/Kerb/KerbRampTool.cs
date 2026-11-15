#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Grading.Kerb;

/// <summary>
/// Builds the solids that, taken away from a footway, leave the dropped kerb: each one has the new ramp or flare surface as its
/// underside, a flat top above the footway, and vertical sides.
/// </summary>
internal static class KerbRampTool
{
    /// <summary>One solid for each clear polygon of the ramp, in model coordinates. <paramref name="topZ"/> is the level of the flat top.</summary>
    public static List<Solid> Build(KerbRampResult ramp, double topZ, List<string> warnings)
    {
        var tools = new List<Solid>();
        foreach (var polygon in ramp.ClearPolygons)
        {
            var solid = BuildOne(polygon, topZ);
            if (solid is null)
            {
                warnings.Add("Part of the ramp could not be turned into a cut, so it was left out.");
                continue;
            }

            tools.Add(solid);
        }

        return tools;
    }

    private static Solid? BuildOne(IReadOnlyList<GradePoint> polygon, double topZ)
    {
        // the outline's own corners only: a repeated corner would stop it being triangulated
        var outline = new List<GradePoint>();
        foreach (var point in polygon)
        {
            if (outline.Count == 0 || PlanMath.Distance(outline[outline.Count - 1], point) > 1e-6)
            {
                outline.Add(point);
            }
        }

        while (outline.Count > 1 && PlanMath.Distance(outline[0], outline[outline.Count - 1]) <= 1e-6)
        {
            outline.RemoveAt(outline.Count - 1);
        }

        if (outline.Count < 3)
        {
            return null;
        }

        var plan = outline.Select(p => new PlanPoint(p.X, p.Y)).ToList();
        if (TJunctionSurface.SignedArea(plan) < 0.0)
        {
            outline.Reverse();
            plan.Reverse();
        }

        var triangles = TJunctionSurface.Triangulate(plan);
        if (triangles is null)
        {
            return null;
        }

        try
        {
            var builder = new TessellatedShapeBuilder();
            builder.OpenConnectedFaceSet(true);

            XYZ Bottom(int i) => new(outline[i].X, outline[i].Y, outline[i].Z);
            XYZ Top(int i) => new(outline[i].X, outline[i].Y, topZ);

            foreach (var t in triangles)
            {
                // top faces up, the underside (the new ground) faces down
                AddFace(builder, Top(t[0]), Top(t[1]), Top(t[2]));
                AddFace(builder, Bottom(t[0]), Bottom(t[2]), Bottom(t[1]));
            }

            for (var i = 0; i < outline.Count; i++)
            {
                var j = (i + 1) % outline.Count;
                AddFace(builder, Bottom(i), Bottom(j), Top(j));
                AddFace(builder, Bottom(i), Top(j), Top(i));
            }

            builder.CloseConnectedFaceSet();
            builder.Target = TessellatedShapeBuilderTarget.Solid;
            builder.Fallback = TessellatedShapeBuilderFallback.Abort;
            builder.Build();

            return builder.GetBuildResult().GetGeometricalObjects().OfType<Solid>().FirstOrDefault(s => s.Volume > 1e-9);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void AddFace(TessellatedShapeBuilder builder, XYZ a, XYZ b, XYZ c)
    {
        builder.AddFace(new TessellatedFace(new List<XYZ> { a, b, c }, ElementId.InvalidElementId));
    }
}
#endif
