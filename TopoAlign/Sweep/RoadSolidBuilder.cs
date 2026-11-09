using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>Lofts a run of section loops into solids.</summary>
internal static class RoadSolidBuilder
{
    /// <summary>
    /// Builds solids between consecutive loops. Loops are lofted <paramref name="groupSize"/> at a time (neighbouring groups
    /// share a loop so the surface is continuous); if a group can't be built it falls back to one span at a time, and a span
    /// that still fails is skipped and counted, so one bad section doesn't lose the whole road.
    /// </summary>
    public static SolidResult Build(IReadOnlyList<CurveLoop> loops, int groupSize = 2)
    {
        var result = new SolidResult();
        if (loops is null || loops.Count < 2)
        {
            return result;
        }

        var size = Math.Max(2, groupSize);
        for (var start = 0; start < loops.Count - 1; start += size - 1)
        {
            var count = Math.Min(size, loops.Count - start);
            var group = loops.Skip(start).Take(count).ToList();

            if (count == 2 || !TryLoft(group, result))
            {
                for (var i = 0; i < group.Count - 1; i++)
                {
                    if (!TryLoft(new List<CurveLoop> { group[i], group[i + 1] }, result))
                    {
                        result.SkippedSpans++;
                    }
                }
            }
        }

        return result;
    }

    private static bool TryLoft(IList<CurveLoop> loops, SolidResult result)
    {
        try
        {
            var options = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
            var solid = GeometryCreationUtilities.CreateLoftGeometry(loops, options);
            if (solid is null || solid.Volume < 1e-9)
            {
                return false;
            }

            result.Solids.Add(solid);
            return true;
        }
        catch (Exception)
        {
            // Revit throws for self-intersecting or degenerate lofts, for example on a radius tighter than the road is wide
            return false;
        }
    }
}
