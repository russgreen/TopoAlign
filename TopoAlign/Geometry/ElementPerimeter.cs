using Autodesk.Revit.DB;

namespace TopoAlign.Geometry;

/// <summary>Finds the outline of an element in plan: the outer edge loop of its highest upward-facing face.</summary>
public static class ElementPerimeter
{
    private const int MaxDepth = 5;

    /// <summary>The curves of the outer loop of the element's highest top face, in order round the loop; empty if it has no top face.</summary>
    public static IReadOnlyList<Curve> GetOuterLoop(Element element)
    {
        var options = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
        var geometry = element.get_Geometry(options);
        if (geometry is null)
        {
            return Array.Empty<Curve>();
        }

        List<Curve>? best = null;
        var bestHeight = double.MinValue;
        Collect(geometry, ref best, ref bestHeight, 0);
        return best ?? (IReadOnlyList<Curve>)Array.Empty<Curve>();
    }

    private static void Collect(GeometryElement geometry, ref List<Curve>? best, ref double bestHeight, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (var geometryObject in geometry)
        {
            if (geometryObject is Solid solid && !solid.Faces.IsEmpty)
            {
                foreach (Face face in solid.Faces)
                {
                    if (!GeometryCalculation.IsTopFace(face))
                    {
                        continue;
                    }

                    var loop = OuterLoop(face);
                    if (loop is null)
                    {
                        continue;
                    }

                    var height = loop.Max(curve => Math.Max(curve.GetEndPoint(0).Z, curve.GetEndPoint(1).Z));
                    if (height > bestHeight)
                    {
                        bestHeight = height;
                        best = loop;
                    }
                }
            }
            else if (geometryObject is GeometryInstance instance)
            {
                Collect(instance.GetInstanceGeometry(), ref best, ref bestHeight, depth + 1);
            }
        }
    }

    // the loop enclosing the most plan area is the outer one; the others are holes
    private static List<Curve>? OuterLoop(Face face)
    {
        List<Curve>? outer = null;
        var largest = double.MinValue;

        foreach (EdgeArray edgeLoop in face.EdgeLoops)
        {
            var curves = new List<Curve>();
            var xs = new List<double>();
            var ys = new List<double>();
            foreach (Edge edge in edgeLoop)
            {
                var curve = edge.AsCurveFollowingFace(face);
                curves.Add(curve);
                foreach (var point in curve.Tessellate())
                {
                    xs.Add(point.X);
                    ys.Add(point.Y);
                }
            }

            if (curves.Count == 0)
            {
                continue;
            }

            var area = Math.Abs(Shoelace(xs, ys));
            if (area > largest)
            {
                largest = area;
                outer = curves;
            }
        }

        return outer;
    }

    private static double Shoelace(List<double> xs, List<double> ys)
    {
        var sum = 0.0;
        for (var i = 0; i < xs.Count; i++)
        {
            var j = (i + 1) % xs.Count;
            sum += xs[i] * ys[j] - xs[j] * ys[i];
        }

        return 0.5 * sum;
    }
}
