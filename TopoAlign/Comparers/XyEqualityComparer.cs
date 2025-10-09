using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TopoAlign.Helpers;

namespace TopoAlign.Comparers;

internal class XyEqualityComparer : IEqualityComparer<XYZ>
{
    private const double _sixteenthInchInFeet = 1.0d / (16.0d * 12.0d);

    public bool Equals(XYZ p, XYZ q)
    {
        var testPoint = new XYZ(q.X, q.Y, p.Z);

        return p.IsAlmostEqualTo(testPoint, _sixteenthInchInFeet);
    }


    public int GetHashCode(XYZ p)
    {
        return StringFormatting.PointString(p).GetHashCode();
    }
}