using System.Collections.Generic;
using Autodesk.Revit.DB;
using TopoAlign.Helpers;

namespace TopoAlign.Comparers;

internal class XyzEqualityComparer : IEqualityComparer<XYZ>
{
    private const double _sixteenthInchInFeet = TopoAlign.Geometry.Tolerances.SixteenthInchInFeet;

    public bool Equals(XYZ? p, XYZ? q)
    {
        if (p is null || q is null)
        {
            return ReferenceEquals(p, q);
        }

        return p.IsAlmostEqualTo(q, _sixteenthInchInFeet);
    }

    public int GetHashCode(XYZ p)
    {
        return StringFormatting.PointString(p).GetHashCode();
    }

}
