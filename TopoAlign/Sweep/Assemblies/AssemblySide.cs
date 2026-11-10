using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Grading;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
#endif

namespace TopoAlign.Sweep.Assemblies;

/// <summary>Which side of the alignment an assembly is graded on.</summary>
internal enum AssemblySide
{
    /// <summary>To the left of the direction of travel (negative lateral offsets).</summary>
    Left,

    /// <summary>To the right of the direction of travel (positive lateral offsets).</summary>
    Right
}
