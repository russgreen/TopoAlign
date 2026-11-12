using Autodesk.Revit.DB;

namespace TopoAlign.Alignments.Plan;

/// <summary>Reads the level of a picked road element straight below/above a plan position, by casting a vertical ray at it.</summary>
internal sealed class RoadElementLevel
{
    private const double RayHeight = 1000.0; // feet above the point the ray starts from

    private readonly ReferenceIntersector _intersector;
    private readonly Transform _sharedToInternal;
    private readonly Transform _internalToShared;

    private RoadElementLevel(ReferenceIntersector intersector, Transform sharedToInternal)
    {
        _intersector = intersector;
        _sharedToInternal = sharedToInternal;
        _internalToShared = sharedToInternal.Inverse;
    }

    /// <summary>Null if the document has no 3D view to cast rays in.</summary>
    public static RoadElementLevel? TryCreate(Document document, ElementId roadId)
    {
        var view3D = document.ActiveView as View3D;
        if (view3D is null || view3D.IsTemplate)
        {
            view3D = new FilteredElementCollector(document).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(v => !v.IsTemplate);
        }

        if (view3D is null)
        {
            return null;
        }

        var intersector = new ReferenceIntersector(roadId, FindReferenceTarget.Face, view3D);
        return new RoadElementLevel(intersector, document.ActiveProjectLocation.GetTotalTransform());
    }

    /// <summary>The element's top level, in shared elevation, at a plan point in shared coordinates; null where the ray misses it.</summary>
    public double? LevelAt(PlanPoint point)
    {
        var origin = _sharedToInternal.OfPoint(new XYZ(point.X, point.Y, 0.0));
        var start = new XYZ(origin.X, origin.Y, origin.Z + RayHeight);
        var hit = _intersector.FindNearest(start, -XYZ.BasisZ);
        if (hit is null)
        {
            return null;
        }

        var top = hit.GetReference().GlobalPoint;
        return _internalToShared.OfPoint(top).Z;
    }
}
