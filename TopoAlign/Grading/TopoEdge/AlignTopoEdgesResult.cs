using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Grading.TopoEdge;

internal sealed class AlignTopoEdgesResult
{
    public int AddedToFirst { get; set; }
    public int AddedToSecond { get; set; }

    /// <summary>Existing points of the first topography that were moved to the height of the second topography's.</summary>
    public int MovedOnFirst { get; set; }

    public int SharedPoints { get; set; }
}
