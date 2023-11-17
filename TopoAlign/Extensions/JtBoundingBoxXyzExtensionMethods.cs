﻿using Autodesk.Revit.DB;

namespace TopoAlign.Extensions;
internal static class JtBoundingBoxXyzExtensionMethods
{
    /// <summary>
    /// Make this bounding box empty by setting the
    /// Min value to plus infinity and Max to minus.
    /// </summary>
    public static void Clear(
      this BoundingBoxXYZ bb)
    {
        double infinity = double.MaxValue;
        bb.Min = new XYZ(infinity, infinity, infinity);
        bb.Max = -bb.Min;
    }

    /// <summary>
    /// Expand the given bounding box to include 
    /// and contain the given point.
    /// </summary>
    public static void ExpandToContain(
      this BoundingBoxXYZ bb,
      XYZ p)
    {
        bb.Min = new XYZ(Math.Min(bb.Min.X, p.X),
          Math.Min(bb.Min.Y, p.Y),
          Math.Min(bb.Min.Z, p.Z));

        bb.Max = new XYZ(Math.Max(bb.Max.X, p.X),
          Math.Max(bb.Max.Y, p.Y),
          Math.Max(bb.Max.Z, p.Z));
    }

    /// <summary>
    /// Expand the given bounding box to include 
    /// and contain the given points.
    /// </summary>
    public static void ExpandToContain(
      this BoundingBoxXYZ bb,
      IEnumerable<XYZ> pts)
    {
        bb.ExpandToContain(new XYZ(
          pts.Min<XYZ, double>(p => p.X),
          pts.Min<XYZ, double>(p => p.Y),
          pts.Min<XYZ, double>(p => p.Z)));

        bb.ExpandToContain(new XYZ(
          pts.Max<XYZ, double>(p => p.X),
          pts.Max<XYZ, double>(p => p.Y),
          pts.Max<XYZ, double>(p => p.Z)));
    }

    /// <summary>
    /// Expand the given bounding box to include 
    /// and contain the given other one.
    /// </summary>
    public static void ExpandToContain(
      this BoundingBoxXYZ bb,
      BoundingBoxXYZ other)
    {
        bb.ExpandToContain(other.Min);
        bb.ExpandToContain(other.Max);
    }
}