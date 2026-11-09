using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>Turns a section and a path into the section's loop standing at a point of the path.</summary>
internal static class SectionLoopFactory
{
    /// <summary>The section as a closed loop of Revit curves at <paramref name="centre"/>, across the horizontal direction <paramref name="tangent"/>.</summary>
    /// <param name="uScale">Stretches the section across the path; a kerb at a mitred corner is widened so it keeps its true width.</param>
    public static CurveLoop CreateLoop(SectionProfile profile, XYZ centre, XYZ tangent, double verticalOffset, double uScale = 1.0)
    {
        var origin = new PathPoint(centre.X, centre.Y, centre.Z);

        XYZ ToWorld(PlanPoint point)
        {
            var world = SectionFrame.ToWorld(point.X * uScale, point.Y, origin, tangent.X, tangent.Y, verticalOffset);
            return new XYZ(world.X, world.Y, world.Z);
        }

        // each vertex is computed once and shared, so neighbouring curves meet exactly
        var vertices = profile.Segments.Select(s => ToWorld(s.StartPoint)).ToList();
        var curves = new List<Curve>(profile.Segments.Count);
        for (var i = 0; i < profile.Segments.Count; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Count];
            var segment = profile.Segments[i];

            if (segment is PlanArc arc)
            {
                curves.Add(Arc.Create(start, end, ToWorld(arc.PointAt(arc.Length / 2.0))));
            }
            else
            {
                curves.Add(Line.CreateBound(start, end));
            }
        }

        return CurveLoop.Create(curves);
    }
}
