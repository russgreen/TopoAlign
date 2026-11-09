using Autodesk.Revit.DB;

namespace TopoAlign.Alignments;

internal sealed class ChainResult
{
    public List<Curve> Curves { get; } = new();

    public double TotalLength { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsContinuous => Curves.Count > 0 && string.IsNullOrEmpty(ErrorMessage);
}
