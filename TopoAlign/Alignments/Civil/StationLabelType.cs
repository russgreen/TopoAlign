using Autodesk.Revit.DB;

namespace TopoAlign.Alignments.Civil;

/// <summary>A spot-dimension type that can be used for alignment station labels.</summary>
internal sealed class StationLabelType
{
    public StationLabelType(ElementId id, string name, bool isRecommended)
    {
        Id = id;
        Name = name;
        IsRecommended = isRecommended;
    }

    public ElementId Id { get; }
    public string Name { get; }

    /// <summary>Autodesk's recommended kind for label sets: rotates with the alignment, vertical text, shows the station, heavy tick leader.</summary>
    public bool IsRecommended { get; }
}
