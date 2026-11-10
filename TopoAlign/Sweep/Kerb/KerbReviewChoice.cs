using Autodesk.Revit.DB;
using Serilog;

namespace TopoAlign.Sweep.Kerb;

/// <summary>What the user decided about a kerb shown for review.</summary>
internal enum KerbReviewChoice
{
    Keep,

    /// <summary>Remove the kerb and build it again on the other side of the edges.</summary>
    Flip,

    Cancel
}
