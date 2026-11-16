using Autodesk.Revit.DB;

namespace TopoAlign.Geometry;

/// <summary>
/// Builds plan (UV) footprint loops from the downward facing faces of an element's geometry.
/// </summary>
public static class FootprintBuilder
{
    // Maximum length (feet) of a sampled segment on a curved edge.
    private const double CurveSegmentLength = 0.1d;
    private const int MaxCurveSegments = 500;
    private const double VertexTolerance = 1e-7d;

    /// <summary>
    /// One entry per bottom face; each entry holds that face's loops (outer loop and any holes).
    /// </summary>
    public static List<List<UVArray>> GetFaceLoops(GeometryElement geometryElement)
    {
        var result = new List<List<UVArray>>();
        Collect(geometryElement, result, 0);
        return result;
    }

    private static void Collect(GeometryElement geometryElement, List<List<UVArray>> result, int depth)
    {
        if (geometryElement == null || depth > 5)
        {
            return;
        }

        foreach (GeometryObject geometryObject in geometryElement)
        {
            if (geometryObject is Solid solid)
            {
                AddSolid(solid, result);
            }
            else if (geometryObject is GeometryInstance instance)
            {
                Collect(instance.GetInstanceGeometry(), result, depth + 1);
            }
        }
    }

    private static void AddSolid(Solid solid, List<List<UVArray>> result)
    {
        if (solid.Faces.IsEmpty)
        {
            return;
        }

        foreach (Face face in solid.Faces)
        {
            if (!GeometryCalculation.IsBottomFace(face))
            {
                continue;
            }

            var loops = new List<UVArray>();
            foreach (EdgeArray edgeLoop in face.EdgeLoops)
            {
                var uvs = GetLoop(edgeLoop, face);
                if (uvs.Count >= 3)
                {
                    loops.Add(new UVArray(uvs));
                }
            }

            if (loops.Count > 0)
            {
                result.Add(loops);
            }
        }
    }

    private static List<UV> GetLoop(EdgeArray edgeLoop, Face face)
    {
        var uvs = new List<UV>();

        foreach (Edge edge in edgeLoop)
        {
            // Oriented to follow the loop direction of the face.
            Curve curve = edge.AsCurveFollowingFace(face);
            foreach (XYZ p in Sample(curve))
            {
                var uv = PointsUtils.Flatten(p);
                if (uvs.Count == 0 || !SameVertex(uvs[uvs.Count - 1], uv))
                {
                    uvs.Add(uv);
                }
            }
        }

        // Drop the closing duplicate.
        while (uvs.Count > 1 && SameVertex(uvs[0], uvs[uvs.Count - 1]))
        {
            uvs.RemoveAt(uvs.Count - 1);
        }

        return uvs;
    }

    private static IEnumerable<XYZ> Sample(Curve curve)
    {
        if (curve is Line)
        {
            return new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) };
        }

        int segments = (int)Math.Ceiling(curve.ApproximateLength / CurveSegmentLength);
        segments = Math.Max(curve.Tessellate().Count - 1, segments);
        segments = Math.Min(Math.Max(segments, 4), MaxCurveSegments);

        var points = new List<XYZ>(segments + 1);
        if (curve.IsBound)
        {
            for (int i = 0; i <= segments; i++)
            {
                points.Add(curve.Evaluate((double)i / segments, true));
            }
        }
        else
        {
            points.AddRange(curve.Tessellate());
        }

        return points;
    }

    private static bool SameVertex(UV a, UV b)
    {
        return Math.Abs(a.U - b.U) <= VertexTolerance && Math.Abs(a.V - b.V) <= VertexTolerance;
    }
}
