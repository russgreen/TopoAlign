Imports System.Diagnostics
Imports System.Collections.Generic
Imports Autodesk.Revit.DB
Imports Quadrant = System.Int32

Public Class PointInPoly
    ''' <summary>
    ''' Determine the quadrant of a polygon vertex 
    ''' relative to the test point.
    ''' </summary>
    Private Shared Function GetQuadrant(vertex As UV, p As UV) As Quadrant
        Return If((vertex.U > p.U), (If((vertex.V > p.V), 0, 3)), (If((vertex.V > p.V), 1, 2)))
    End Function

    ''' <summary>
    ''' Determine the X intercept of a polygon edge 
    ''' with a horizontal line at the Y value of the 
    ''' test point.
    ''' </summary>
    Private Shared Function X_intercept(p As UV, q As UV, y As Double) As Double
        Debug.Assert(0 <> (p.V - q.V), "unexpected horizontal segment")

        Return q.U - ((q.V - y) * ((p.U - q.U) / (p.V - q.V)))
    End Function

    Private Shared Sub AdjustDelta(ByRef delta As Integer, vertex As UV, next_vertex As UV, p As UV)
        Select Case delta
            ' make quadrant deltas wrap around:
            Case 3
                delta = -1
                Exit Select
            Case -3
                delta = 1
                Exit Select
                ' check if went around point cw or ccw:
            Case 2, -2
                If X_intercept(vertex, next_vertex, p.V) > p.U Then
                    delta = -delta
                End If
                Exit Select
        End Select
    End Sub

    ''' <summary>
    ''' Determine whether given 2D point lies within 
    ''' the polygon.
    ''' 
    ''' Written by Jeremy Tammik, Autodesk, 2009-09-23, 
    ''' based on code that I wrote back in 1996 in C++, 
    ''' which in turn was based on C code from the 
    ''' article "An Incremental Angle Point in Polygon 
    ''' Test" by Kevin Weiler, Autodesk, in "Graphics 
    ''' Gems IV", Academic Press, 1994.
    ''' 
    ''' Copyright (C) 2009 by Jeremy Tammik. All 
    ''' rights reserved.
    ''' 
    ''' This code may be freely used. Please preserve 
    ''' this comment.
    ''' </summary>
    'Public Shared Function PolygonContains(polygon As List(Of UV), point As UV) As Boolean
    '    ' initialize
    '    Dim quad As Quadrant = GetQuadrant(polygon.Item(0), point)

    '    Dim angle As Quadrant = 0

    '    ' loop on all vertices of polygon
    '    Dim next_quad As Quadrant, delta As Quadrant
    '    Dim n As Integer = polygon.Count
    '    For i As Integer = 0 To n - 1
    '        Dim vertex As UV = polygon.Item(i)

    '        Dim next_vertex As UV = polygon.Item(If((i + 1 < n), i + 1, 0))

    '        ' calculate quadrant and delta from last quadrant

    '        next_quad = GetQuadrant(next_vertex, point)
    '        delta = next_quad - quad

    '        AdjustDelta(delta, vertex, next_vertex, point)

    '        ' add delta to total angle sum
    '        angle = angle + delta

    '        ' increment for next step
    '        quad = next_quad
    '    Next

    '    ' complete 360 degrees (angle of + 4 or -4 ) 
    '    ' means inside

    '    Return (angle = +4) OrElse (angle = -4)

    '    ' odd number of windings rule:
    '    ' if (angle & 4) return INSIDE; else return OUTSIDE;
    '    ' non-zero winding rule:
    '    ' if (angle != 0) return INSIDE; else return OUTSIDE;
    'End Function
    Public Shared Function PolygonContains(polygon As UVArray, point As UV) As Boolean
        ' initialize
        Dim quad As Quadrant = GetQuadrant(polygon.Item(0), point)

        Dim angle As Quadrant = 0

        ' loop on all vertices of polygon
        Dim next_quad As Quadrant
        Dim delta As Quadrant
        Dim n As Integer = polygon.Size

        For i As Integer = 0 To n - 1
            Dim vertex As UV = polygon.Item(i)

            Dim next_vertex As UV = polygon.Item(If((i + 1 < n), i + 1, 0))

            ' calculate quadrant and delta from last quadrant
            next_quad = GetQuadrant(next_vertex, point)
            delta = next_quad - quad

            AdjustDelta(delta, vertex, next_vertex, point)

            ' add delta to total angle sum
            angle = angle + delta

            ' increment for next step
            quad = next_quad
        Next

        ' complete 360 degrees (angle of + 4 or -4 ) 
        ' means inside

        Return (angle = +4) OrElse (angle = -4)

        ' odd number of windings rule:
        ' if (angle & 4) return INSIDE; else return OUTSIDE;
        ' non-zero winding rule:
        ' if (angle != 0) return INSIDE; else return OUTSIDE;
    End Function

    Public Shared Function PointInPolygon(polygon As UVArray, point As UV) As Boolean
        ' Get the angle between the point and the
        ' first and last vertices.
        Dim max_point As Integer = polygon.Size - 1
        Dim total_angle As Single = GetAngle(polygon.Item(max_point).U, polygon.Item(max_point).V, point.U, point.V, polygon.Item(0).U, polygon.Item(0).V)

        ' Add the angles from the point
        ' to each other pair of vertices.
        For i As Integer = 0 To max_point - 1
            total_angle += GetAngle(polygon.Item(i).U, polygon.Item(i).V, point.U, point.V, polygon.Item(i + 1).U, polygon.Item(i + 1).V)
        Next

        ' The total angle should be 2 * PI or -2 * PI if
        ' the point is in the polygon and close to zero
        ' if the point is outside the polygon.
        Return Math.Abs(total_angle) > 0.000001
    End Function

    ' Return True if the point is in the polygon.
    Public Shared Function PointInPolygon1(ByVal points() As Drawing.PointF, _
        ByVal X As Single, ByVal Y As Single) As Boolean
        ' Get the angle between the point and the
        ' first and last vertices.
        Dim max_point As Integer = points.Length - 1
        Dim total_angle As Single = GetAngle( _
            points(max_point).X, points(max_point).Y, _
            X, Y, _
            points(0).X, points(0).Y)

        ' Add the angles from the point
        ' to each other pair of vertices.
        For i As Integer = 0 To max_point - 1
            total_angle += GetAngle( _
                points(i).X, points(i).Y, _
                X, Y, _
                points(i + 1).X, points(i + 1).Y)
        Next i

        ' The total angle should be 2 * PI or -2 * PI if
        ' the point is in the polygon and close to zero
        ' if the point is outside the polygon.
        Return Math.Abs(total_angle) > 0.0000001
    End Function

    ' Return the angle ABC.
    ' Return a value between PI and -PI.
    ' Note that the value is the opposite of what you might
    ' expect because Y coordinates increase downward.
    Private Shared Function GetAngle(ByVal Ax As Single, ByVal Ay As _
        Single, ByVal Bx As Single, ByVal By As Single, ByVal _
        Cx As Single, ByVal Cy As Single) As Single
        Dim dot_product As Single
        Dim cross_product As Single

        ' Get the dot product and cross product.
        dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy)
        cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, _
            Cy)

        ' Calculate the angle.
        GetAngle = ATan2(cross_product, dot_product)
    End Function

    Private Shared Function DotProduct( _
    ByVal Ax As Single, ByVal Ay As Single, _
    ByVal Bx As Single, ByVal By As Single, _
    ByVal Cx As Single, ByVal Cy As Single _
  ) As Single
        Dim BAx As Single
        Dim BAy As Single
        Dim BCx As Single
        Dim BCy As Single

        ' Get the vectors' coordinates.
        BAx = Ax - Bx
        BAy = Ay - By
        BCx = Cx - Bx
        BCy = Cy - By

        ' Calculate the dot product.
        DotProduct = BAx * BCx + BAy * BCy
    End Function

    Private Shared Function CrossProductLength( _
    ByVal Ax As Single, ByVal Ay As Single, _
    ByVal Bx As Single, ByVal By As Single, _
    ByVal Cx As Single, ByVal Cy As Single _
  ) As Single
        Dim BAx As Single
        Dim BAy As Single
        Dim BCx As Single
        Dim BCy As Single

        ' Get the vectors' coordinates.
        BAx = Ax - Bx
        BAy = Ay - By
        BCx = Cx - Bx
        BCy = Cy - By

        ' Calculate the Z coordinate of the cross product.
        CrossProductLength = BAx * BCy - BAy * BCx
    End Function

    Private Shared Function ATan2(ByVal opp As Single, ByVal adj As _
    Single) As Single
        Dim angle As Single

        ' Get the basic angle.
        If Math.Abs(adj) < 0.0001 Then
            angle = Math.PI / 2
        Else
            angle = Math.Abs(Math.Atan(opp / adj))
        End If

        ' See if we are in quadrant 2 or 3.
        If adj < 0 Then
            ' angle > PI/2 or angle < -PI/2.
            angle = Math.PI - angle
        End If

        ' See if we are in quadrant 3 or 4.
        If opp < 0 Then
            angle = -angle
        End If

        ' Return the result.
        ATan2 = angle
    End Function


End Class

Public Class UVArray
    Private arrayPoints As List(Of UV)
    Public Sub New(XYZArray As List(Of XYZ))
        arrayPoints = New List(Of UV)()
        For Each p As XYZ In XYZArray
            arrayPoints.Add(Util.Flatten(p))
        Next
    End Sub

    Public Function Item(i As Integer) As UV
        Return arrayPoints(i)
    End Function

    Public ReadOnly Property Size() As Integer
        Get
            Return arrayPoints.Count
        End Get
    End Property

End Class
