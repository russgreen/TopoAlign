using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign.Alignments;

/// <summary>Lets the user pick curves on imported or linked CAD (DWG/DXF) instances.</summary>
internal sealed class CadCurvePickFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem is ImportInstance;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return true;
    }
}

/// <summary>Reads the lines, arcs and polylines a user picked on CAD imports as Revit curves in model coordinates.</summary>
internal static class CadCurveReader
{
    /// <summary>
    /// Curves for the picked references. Revit doesn't document which coordinate space geometry fetched from a
    /// reference is in, so both the raw geometry and the geometry transformed by the import instance are tried and the
    /// one that lies on the point the user clicked is used.
    /// </summary>
    /// <param name="error">Why nothing could be read (unsupported curve type, not CAD), or null on success.</param>
    public static List<Curve> Read(Document document, IEnumerable<Reference> references, out string? error)
    {
        error = null;
        var curves = new List<Curve>();

        foreach (var reference in references)
        {
            if (document.GetElement(reference) is not ImportInstance import)
            {
                error = "Pick lines, arcs or polylines on an imported or linked CAD drawing.";
                return new List<Curve>();
            }

            var geometry = import.GetGeometryObjectFromReference(reference);
            var raw = ToCurves(geometry, out var unsupported);
            if (raw.Count == 0)
            {
                error = unsupported is null
                    ? "Nothing could be read from the picked CAD geometry."
                    : $"A {unsupported} can't be used for an alignment. Pick lines, arcs or polylines.";
                return new List<Curve>();
            }

            curves.AddRange(ChooseCoordinateSpace(raw, import.GetTransform(), reference.GlobalPoint));
        }

        return curves;
    }

    /// <summary>Line, Arc and PolyLine become curves; anything else reports its type in <paramref name="unsupported"/>.</summary>
    internal static List<Curve> ToCurves(GeometryObject geometry, out string? unsupported)
    {
        unsupported = null;
        var curves = new List<Curve>();

        switch (geometry)
        {
            case Line:
            case Arc:
                curves.Add((Curve)geometry);
                break;

            case PolyLine polyline:
                var points = polyline.GetCoordinates();
                for (var i = 1; i < points.Count; i++)
                {
                    if (points[i - 1].DistanceTo(points[i]) > 1e-6)
                    {
                        curves.Add(Line.CreateBound(points[i - 1], points[i]));
                    }
                }

                break;

            case Curve other:
                unsupported = other.GetType().Name.ToLowerInvariant();
                break;

            case null:
                break;

            default:
                unsupported = geometry.GetType().Name.ToLowerInvariant();
                break;
        }

        return curves;
    }

    private static List<Curve> ChooseCoordinateSpace(List<Curve> raw, Transform instanceTransform, XYZ clicked)
    {
        if (instanceTransform is null || instanceTransform.IsIdentity || clicked is null)
        {
            return raw;
        }

        var transformed = raw.Select(c => c.CreateTransformed(instanceTransform)).ToList();
        return DistanceInPlan(transformed, clicked) < DistanceInPlan(raw, clicked) ? transformed : raw;
    }

    /// <summary>Smallest plan distance from a point to a set of curves, measured to their tessellation.</summary>
    internal static double DistanceInPlan(IEnumerable<Curve> curves, XYZ point)
    {
        var best = double.MaxValue;
        foreach (var curve in curves)
        {
            var vertices = curve.Tessellate();
            for (var i = 1; i < vertices.Count; i++)
            {
                best = Math.Min(best, DistanceToSegment(point, vertices[i - 1], vertices[i]));
            }
        }

        return best;
    }

    private static double DistanceToSegment(XYZ p, XYZ a, XYZ b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lengthSquared = abx * abx + aby * aby;
        var t = lengthSquared <= 1e-18 ? 0.0 : ((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lengthSquared;
        t = Math.Max(0.0, Math.Min(1.0, t));
        var dx = p.X - (a.X + abx * t);
        var dy = p.Y - (a.Y + aby * t);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
