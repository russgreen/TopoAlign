using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ARCHISOFT_topoalign
{
    class XyzEqualityComparer : IEqualityComparer<XYZ>
    {

        // Private _eps As Double


        // Public Sub New(eps As Double)
        // Debug.Assert(0 < eps, "expected a positive tolerance")

        // _eps = eps
        // End Sub

        // Public Function Equals1(x As XYZ, y As XYZ) As Boolean Implements IEqualityComparer(Of XYZ).Equals
        // Return _eps > x.DistanceTo(y)
        // End Function

        // Public Function GetHashCode1(obj As XYZ) As Integer Implements IEqualityComparer(Of XYZ).GetHashCode
        // Return Util.PointString(obj).GetHashCode()
        // End Function

        private const double _sixteenthInchInFeet = 1.0d / (16.0d * 12.0d);
        // Private m_useHighest As Boolean

        // Public Sub New(Optional ByRef useHighest As Boolean = True)
        // m_useHighest = useHighest
        // End Sub

        public bool Equals(XYZ p, XYZ q)
        {
            return p.IsAlmostEqualTo(q, _sixteenthInchInFeet);
        }

        public int GetHashCode(XYZ p)
        {
            return Util.PointString(p).GetHashCode();
        }
    }
}