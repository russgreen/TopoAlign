using Autodesk.Revit.DB;
using TopoAlign.Alignments.AlignmentJson;

namespace TopoAlign.Alignments.Plan;

/// <summary>Converts picked Revit curves into the Revit-free plan segments the alignment layout works with.</summary>
internal static class PlanSegmentAdapter
{
    /// <summary>Lines and arcs only; Z is dropped. Throws <see cref="NotSupportedException"/> for any other curve.</summary>
    public static List<PlanSegment> FromCurves(IEnumerable<Curve> curves)
    {
        if (curves is null)
        {
            throw new ArgumentNullException(nameof(curves));
        }

        var segments = new List<PlanSegment>();
        foreach (var curve in curves)
        {
            switch (curve)
            {
                case Line line:
                    segments.Add(new PlanLine(
                        new PlanPoint(line.GetEndPoint(0).X, line.GetEndPoint(0).Y),
                        new PlanPoint(line.GetEndPoint(1).X, line.GetEndPoint(1).Y)));
                    break;

                case Arc arc:
                    var (center, radius, sweep, clockwise) = AlignmentJsonBuilder.GetArcParameters(arc, Transform.Identity);
                    var start = arc.GetEndPoint(0);
                    var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
                    segments.Add(new PlanArc(new PlanPoint(center.X, center.Y), radius, startAngle, clockwise ? -sweep : sweep));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported curve type '{curve.GetType().Name}'.");
            }
        }

        return segments;
    }
}
