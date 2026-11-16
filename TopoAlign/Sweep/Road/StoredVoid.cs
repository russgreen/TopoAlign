using Autodesk.Revit.DB;

namespace TopoAlign.Sweep.Road;

/// <summary>A void a junction put in a family, as the data to make it again. The points are in the family's coordinates.</summary>
internal sealed class StoredVoid
{
    public StoredVoid(string side, string role, IReadOnlyList<XYZ> points, double height)
    {
        Side = side;
        Role = role;
        Points = points;
        Height = height;
    }

    public string Side { get; }
    public string Role { get; }
    public IReadOnlyList<XYZ> Points { get; }
    public double Height { get; }
}
