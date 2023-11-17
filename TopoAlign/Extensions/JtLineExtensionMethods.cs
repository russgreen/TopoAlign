using Autodesk.Revit.DB;

namespace TopoAlign.Extensions;
internal static class JtLineExtensionMethods
{
    /// <summary>
    /// Return true if the given point is very close 
    /// to this line, within a very narrow ellipse
    /// whose focal points are the line start and end.
    /// The tolerance is defined as (1 - e) using the 
    /// eccentricity e. e = 0 means we have a circle; 
    /// The closer e is to 1, the more elongated the 
    /// shape of the ellipse.
    /// https://en.wikipedia.org/wiki/Ellipse#Eccentricity
    /// </summary>
    public static bool Contains(
      this Line line,
      XYZ p,
      double tolerance = 1.0e-9)
    {
        XYZ a = line.GetEndPoint(0); // line start point
        XYZ b = line.GetEndPoint(1); // line end point
        double f = a.DistanceTo(b); // distance between focal points
        double da = a.DistanceTo(p);
        double db = p.DistanceTo(b);
        // da + db is always greater or equal f
        return ((da + db) - f) * f < tolerance;
    }
}
