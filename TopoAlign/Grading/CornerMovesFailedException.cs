#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading;

/// <summary>
/// Revit would not set the heights of the toposolid's boundary corners. The edit was rolled back completely, so it can be run again
/// without them; carrying on with the same shape editor after a refused edit is not safe and can crash Revit.
/// </summary>
internal sealed class CornerMovesFailedException : InvalidOperationException
{
    public CornerMovesFailedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
#endif
