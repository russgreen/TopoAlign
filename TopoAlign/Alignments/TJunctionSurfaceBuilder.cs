using Autodesk.Revit.DB;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Alignments;

/// <summary>Makes the road surface of a T-junction as a thin DirectShape solid.</summary>
internal static class TJunctionSurfaceBuilder
{
    /// <summary>
    /// Builds a solid whose top follows <paramref name="outline"/> (internal coordinates, counter-clockwise seen from above) and
    /// whose bottom is <paramref name="thickness"/> feet below. Returns null if the outline can't be triangulated.
    /// Must be called inside a transaction.
    /// </summary>
    public static DirectShape? Create(Document document, IReadOnlyList<XYZ> outline, double thickness, BuiltInCategory category, string name)
    {
        var triangles = Triangulate(outline);
        if (triangles is null)
        {
            return null;
        }

        var drop = new XYZ(0.0, 0.0, -Math.Max(thickness, 0.01));
        var builder = new TessellatedShapeBuilder();
        builder.OpenConnectedFaceSet(true);

        foreach (var t in triangles)
        {
            // top, counter-clockwise from above
            AddFace(builder, outline[t[0]], outline[t[1]], outline[t[2]]);

            // bottom, reversed so it faces down
            AddFace(builder, outline[t[0]] + drop, outline[t[2]] + drop, outline[t[1]] + drop);
        }

        // sides: counter-clockwise from outside means top then bottom along each edge
        for (var i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            AddFace(builder, a, a + drop, b + drop);
            AddFace(builder, a, b + drop, b);
        }

        builder.CloseConnectedFaceSet();
        builder.Target = TessellatedShapeBuilderTarget.Solid;
        builder.Fallback = TessellatedShapeBuilderFallback.Abort;
        builder.Build();

        var shape = DirectShape.CreateElement(document, new ElementId(category));
        shape.SetShape(builder.GetBuildResult().GetGeometricalObjects());
        shape.Name = name;
        return shape;
    }

    /// <summary>The triangles (vertex indices, counter-clockwise from above) of an outline, or null if it can't be triangulated.</summary>
    public static List<int[]>? Triangulate(IReadOnlyList<XYZ> outline)
    {
        return TJunctionSurface.Triangulate(outline.Select(p => new PlanPoint(p.X, p.Y)).ToList());
    }

    private static void AddFace(TessellatedShapeBuilder builder, XYZ a, XYZ b, XYZ c)
    {
        builder.AddFace(new TessellatedFace(new List<XYZ> { a, b, c }, ElementId.InvalidElementId));
    }
}
