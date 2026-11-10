using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep.Road;

internal sealed class RoadOptions
{
    /// <summary>Distance between sections along the alignment, in feet.</summary>
    public double Interval { get; set; } = 3.28084;

    /// <summary>Added to the height of every section point, in feet.</summary>
    public double VerticalOffset { get; set; }

    /// <summary>The category the DirectShape is filed under.</summary>
    public BuiltInCategory Category { get; set; } = BuiltInCategory.OST_GenericModel;

    /// <summary>Name of the profile type, kept with the road so it's easy to tell what made it.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Segmented lofts (default) or smooth surfaces without seams between sections.</summary>
    public RoadGeometryMethod GeometryMethod { get; set; } = RoadGeometryMethod.SegmentedLoft;

    /// <summary>A DirectShape (default) or a loadable family with an instance placed in the model.</summary>
    public RoadOutput Output { get; set; } = RoadOutput.DirectShape;

    /// <summary>The toposolid type to use when <see cref="Output"/> is <see cref="RoadOutput.Toposolid"/>.</summary>
    public ElementId ToposolidTypeId { get; set; } = ElementId.InvalidElementId;

    /// <summary>The level to host the toposolid on when <see cref="Output"/> is <see cref="RoadOutput.Toposolid"/>.</summary>
    public ElementId LevelId { get; set; } = ElementId.InvalidElementId;
}
