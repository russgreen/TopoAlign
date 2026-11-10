#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Comparers;

namespace TopoAlign.Grading.GradeToSurface;

/// <summary>The slope never reached the toposolid, so nothing was changed. Not a failure when several toposolids are being tried.</summary>
internal sealed class SlopeMissesToposolidException : InvalidOperationException
{
    public SlopeMissesToposolidException(string message)
        : base(message)
    {
    }
}
#endif
