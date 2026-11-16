using Autodesk.Revit.DB;

namespace TopoAlign.Sweep.Road;

/// <summary>What one family needs to be cut, read from the road part while its element is still valid.</summary>
internal sealed class VoidJob
{
    public VoidJob(string familyId, IReadOnlyList<XYZ> polygon, string role, double minZ, double maxZ, Transform toFamily)
    {
        FamilyName = familyId;
        Polygon = polygon;
        Role = role;
        MinZ = minZ;
        MaxZ = maxZ;
        ToFamily = toFamily;
    }

    public string FamilyName { get; }
    public IReadOnlyList<XYZ> Polygon { get; }
    public string Role { get; }
    public double MinZ { get; }
    public double MaxZ { get; }

    /// <summary>Takes a point in the model into the family's own coordinates (the family is built around the instance's origin).</summary>
    public Transform ToFamily { get; }
}
