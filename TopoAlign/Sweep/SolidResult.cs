using Autodesk.Revit.DB;

namespace TopoAlign.Sweep;

internal sealed class SolidResult
{
    public List<Solid> Solids { get; } = new();
    public int SkippedSpans { get; set; }
}
