Class XyzEqualityComparer
    Implements IEqualityComparer(Of XYZ)

    'Private _eps As Double


    'Public Sub New(eps As Double)
    '    Debug.Assert(0 < eps, "expected a positive tolerance")

    '    _eps = eps
    'End Sub

    'Public Function Equals1(x As XYZ, y As XYZ) As Boolean Implements IEqualityComparer(Of XYZ).Equals
    '    Return _eps > x.DistanceTo(y)
    'End Function

    'Public Function GetHashCode1(obj As XYZ) As Integer Implements IEqualityComparer(Of XYZ).GetHashCode
    '    Return Util.PointString(obj).GetHashCode()
    'End Function

    Const _sixteenthInchInFeet As Double = 1.0 / (16.0 * 12.0)
    'Private m_useHighest As Boolean

    'Public Sub New(Optional ByRef useHighest As Boolean = True)
    '    m_useHighest = useHighest
    'End Sub

    Public Function Equals1(p As XYZ, q As XYZ) As Boolean Implements IEqualityComparer(Of XYZ).Equals
        Return p.IsAlmostEqualTo(q, _sixteenthInchInFeet)
    End Function

    Public Function GetHashCode1(p As XYZ) As Integer Implements IEqualityComparer(Of XYZ).GetHashCode
        Return Util.PointString(p).GetHashCode()
    End Function

End Class
