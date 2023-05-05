using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TopoAlign.Comparers;

class XyzEqualityComparer : IEqualityComparer<XYZ>
{
    private const double _sixteenthInchInFeet = 1.0d / (16.0d * 12.0d);

    public bool Equals(XYZ p, XYZ q)
    {
        return p.IsAlmostEqualTo(q, _sixteenthInchInFeet);
    }

    public int GetHashCode(XYZ p)
    {
        return Util.PointString(p).GetHashCode();
    }
}
