using Autodesk.Revit.DB;
using Serilog;

namespace TopoAlign.Sweep;

internal sealed class KerbOptions
{
    /// <summary>Longest distance between sections on a curved edge, in feet.</summary>
    public double Interval { get; set; } = 3.28084;

    /// <summary>Added to the height of every section point, in feet.</summary>
    public double VerticalOffset { get; set; }

    /// <summary>The category of the kerb.</summary>
    public BuiltInCategory Category { get; set; } = BuiltInCategory.OST_GenericModel;

    /// <summary>Name of the profile type, kept with the kerb so it's easy to tell what made it.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>A DirectShape (default) or a loadable family with an instance placed in the model.</summary>
    public RoadOutput Output { get; set; } = RoadOutput.DirectShape;

    /// <summary>
    /// Called with the kerb in the model but not yet committed, so the user can see it. Returns what to do with it.
    /// Null to keep the kerb without asking.
    /// </summary>
    public Func<KerbResult, KerbReviewChoice>? Review { get; set; }
}
