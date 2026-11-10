using TopoAlign.Alignments;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Sweep.Assemblies;

/// <summary>The outline of one part of an assembly as it stands across the road, in (u, v) feet.</summary>
internal sealed class PreviewOutline
{
    public PreviewOutline(int index, string name, List<PlanPoint> points)
    {
        Index = index;
        Name = name;
        Points = points;
    }

    /// <summary>Position of the part in the assembly, from 0.</summary>
    public int Index { get; }

    public string Name { get; }

    /// <summary>The closed outline, in order; the last point joins back to the first.</summary>
    public List<PlanPoint> Points { get; }
}
