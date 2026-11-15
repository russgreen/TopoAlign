using Autodesk.Revit.DB;
using Serilog;
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
    public static DirectShape? Create(Document document, IReadOnlyList<XYZ> outline, double thickness, BuiltInCategory category, string name,
        string? junctionKey = null)
    {
        // even out the edge lengths first: long edges beside short kerb chords give triangles too thin for Revit to build
        outline = TJunctionSurface.Densify(outline, TJunctionSurface.MaxOutlineEdge,
            (a, b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)),
            (a, b, t) => new XYZ(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t));

        var triangles = Triangulate(outline);
        if (triangles is null)
        {
            return null;
        }

        var drop = new XYZ(0.0, 0.0, -Math.Max(thickness, 0.01));
        IList<GeometryObject> geometry;
        try
        {
            geometry = BuildSolid(outline, triangles, drop);
        }
        catch (Exception ex)
        {
            // keep the numbers in a file the user can send, so a failing layout can be reproduced
            var text = $"{ex.Message}{Environment.NewLine}thickness {thickness}{Environment.NewLine}{outline.Count} outline points, {triangles.Count} triangles{Environment.NewLine}"
                + string.Join(Environment.NewLine, outline.Select(p => $"{p.X:R} {p.Y:R} {p.Z:R}"))
                + Environment.NewLine + "triangles: " + string.Join(" ", triangles.Select(t => $"{t[0]},{t[1]},{t[2]}"));
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TopoAlign-tjunction-surface-failure.txt");
            string? saved = null;
            try
            {
                System.IO.File.WriteAllText(path, text);
                saved = path;
            }
            catch (Exception writeError)
            {
                log.Warning(writeError, "Could not write {path}", path);
            }

            log.Error(ex, "The junction surface solid could not be built: {text}", text);
            throw new InvalidOperationException(saved is null ? ex.Message : $"{ex.Message} The outline was saved to {saved}.", ex);
        }

        var shape = DirectShape.CreateElement(document, new ElementId(category));
        shape.SetShape(geometry);
        shape.Name = name;
        if (junctionKey is not null)
        {
            shape.ApplicationId = ApplicationId;
            shape.ApplicationDataId = junctionKey;
        }

        return shape;
    }

    private static readonly ILogger log = Log.ForContext(typeof(TJunctionSurfaceBuilder));

    private static IList<GeometryObject> BuildSolid(IReadOnlyList<XYZ> outline, List<int[]> triangles, XYZ drop)
    {
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
        return builder.GetBuildResult().GetGeometricalObjects();
    }

    /// <summary>
    /// Lengthens the outline back along the side road by <paramref name="gap"/>, so the surface meets a road that stops short of the
    /// join line. The two outline points on the join line move out along <c>-Direction</c>; their height is kept. Returns the outline
    /// unchanged if the join points can't be found next to each other.
    /// </summary>
    public static IReadOnlyList<XYZ> ExtendBack(IReadOnlyList<XYZ> outline, SideJoin join, double gap)
    {
        if (gap <= 0.0)
        {
            return outline;
        }

        var left = IndexOf(outline, join.Left);
        var right = IndexOf(outline, join.Right);
        var n = outline.Count;
        if (left < 0 || right < 0)
        {
            return outline;
        }

        XYZ Back(XYZ p) => new(p.X - join.Direction.X * gap, p.Y - join.Direction.Y * gap, p.Z);

        // the join edge runs from one of the two points to the other; the new points go between them, nearest their own
        int first;
        int second;
        if ((left + 1) % n == right)
        {
            first = left;
            second = right;
        }
        else if ((right + 1) % n == left)
        {
            first = right;
            second = left;
        }
        else
        {
            return outline;
        }

        var result = new List<XYZ>(n + 2);
        for (var i = 0; i < n; i++)
        {
            result.Add(outline[i]);
            if (i == first)
            {
                result.Add(Back(outline[first]));
                result.Add(Back(outline[second]));
            }
        }

        return result;
    }

    private static int IndexOf(IReadOnlyList<XYZ> outline, PlanPoint point)
    {
        for (var i = 0; i < outline.Count; i++)
        {
            if (Math.Abs(outline[i].X - point.X) < 1e-4 && Math.Abs(outline[i].Y - point.Y) < 1e-4)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Marks the junction surfaces made here. It is deliberately not the road tag, so re-making the side road doesn't delete the junction.
    /// </summary>
    public const string ApplicationId = "TopoAlign.TJunction";

    /// <summary>Deletes the junction surfaces an earlier run made for the junction. Must be called inside a transaction.</summary>
    public static int DeletePrevious(Document document, string junctionKey)
    {
        var ids = new FilteredElementCollector(document)
            .OfClass(typeof(DirectShape))
            .Cast<DirectShape>()
            .Where(shape => shape.ApplicationId == ApplicationId && shape.ApplicationDataId == junctionKey)
            .Select(shape => shape.Id)
            .ToList();
        if (ids.Count > 0)
        {
            document.Delete(ids);
        }

        return ids.Count;
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
