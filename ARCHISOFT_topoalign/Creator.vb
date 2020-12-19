#Region "Header"
'
' Creator.cs - model line creator helper class
'
' Copyright (C) 2008-2013 by Jeremy Tammik,
' Autodesk Inc. All rights reserved.
'
#End Region

#Region "Namespaces"
Imports System.Collections.Generic
Imports System.Diagnostics
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.DB
#End Region

Class Creator
    Private _doc As Document

    ' these are
    ' Autodesk.Revit.Creation
    ' objects!
    Private _creapp As Autodesk.Revit.Creation.Application
    Private _credoc As Autodesk.Revit.Creation.Document

    Public Sub New(doc As Document)
        _doc = doc
        _credoc = doc.Create
        _creapp = doc.Application.Create
    End Sub

    ''' <summary>
    ''' Determine the plane that a given curve resides in and return its normal vector.
    ''' Ask the curve for its start and end points and some curve in the middle.
    ''' The latter can be obtained by asking the curve for its parameter range and
    ''' evaluating it in the middle, or by tessellation. In case of tessellation,
    ''' you could iterate through the tessellation points and use each one together
    ''' with the start and end points to try and determine a valid plane.
    ''' Once one is found, you can add debug assertions to ensure that the other
    ''' tessellation points (if there are any more) are in the same plane.
    ''' In the case of the line, the tessellation only returns two points.
    ''' I once heard that that is the only element that can do that, all
    ''' non-linear curves return at least three. So you could use this property
    ''' to determine that a line is a line (and add an assertion as well, if you like).
    ''' Update, later: please note that the Revit API provides an overload of the
    ''' NewPlane method taking a CurveArray argument.
    ''' </summary>
    Private Function GetCurveNormal(curve As Curve) As XYZ
        Dim pts As IList(Of XYZ) = curve.Tessellate()
        Dim n As Integer = pts.Count

        Debug.Assert(1 < n, "expected at least two points " & "from curve tessellation")

        Dim p As XYZ = pts(0)
        Dim q As XYZ = pts(n - 1)
        Dim v As XYZ = q - p
        Dim w As XYZ, normal As XYZ = Nothing

        If 2 = n Then
            Debug.Assert(TypeOf curve Is Line, "expected non-line element to have " & "more than two tessellation points")

            ' for non-vertical lines, use Z axis to
            ' span the plane, otherwise Y axis:

            Dim dxy As Double = Math.Abs(v.X) + Math.Abs(v.Y)

            w = If((dxy > Util.TolPointOnPlane), XYZ.BasisZ, XYZ.BasisY)

            normal = v.CrossProduct(w).Normalize()
        Else
            Dim i As Integer = 0
            While System.Threading.Interlocked.Increment(i) < n - 1
                w = pts(i) - p
                normal = v.CrossProduct(w)
                If Not normal.IsZeroLength() Then
                    normal = normal.Normalize()
                    Exit While
                End If
            End While

#If DEBUG Then
				If True Then
					Dim normal2 As XYZ
					While System.Threading.Interlocked.Increment(i) < n - 1
						w = pts(i) - p
						normal2 = v.CrossProduct(w)
						Debug.Assert(normal2.IsZeroLength() OrElse Util.IsZero(normal2.AngleTo(normal)), "expected all points of curve to " & "lie in same plane")
					End While
#End If

        End If
        Return normal
    End Function

    ''' <summary>
    ''' Miroslav Schonauer's model line creation method.
    ''' A utility function to create an arbitrary sketch
    ''' plane given the model line end points.
    ''' </summary>
    ''' <param name="app">Revit application</param>
    ''' <param name="p">Model line start point</param>
    ''' <param name="q">Model line end point</param>
    ''' <returns></returns>
    Public Shared Function CreateModelLine(doc As Document, p As XYZ, q As XYZ) As ModelLine
        If p.DistanceTo(q) < Util.MinLineLength Then
            Return Nothing
        End If

        ' Create sketch plane; for non-vertical lines,
        ' use Z-axis to span the plane, otherwise Y-axis:

        Dim v As XYZ = q - p

        Dim dxy As Double = Math.Abs(v.X) + Math.Abs(v.Y)

        Dim w As XYZ = If((dxy > Util.TolPointOnPlane), XYZ.BasisZ, XYZ.BasisY)

        Dim norm As XYZ = v.CrossProduct(w).Normalize()

        Dim creApp As Autodesk.Revit.Creation.Application = doc.Application.Create

#If CONFIG >= "2018" Then
        Dim plane As Plane = Plane.CreateByNormalAndOrigin(norm, p)
#Else
        Dim plane As Plane = creApp.NewPlane(norm, p)
#End If


        Dim creDoc As Autodesk.Revit.Creation.Document = doc.Create

        'SketchPlane sketchPlane = creDoc.NewSketchPlane( plane ); // 2013
        Dim sketchPlane__1 As SketchPlane = SketchPlane.Create(doc, plane)
        ' 2014
        'creApp.NewLine( p, q, true ), // 2013
        ' 2014
        Return TryCast(creDoc.NewModelCurve(Line.CreateBound(p, q), sketchPlane__1), ModelLine)
    End Function

    Private Function NewSketchPlanePassLine(line As Line) As SketchPlane
        Dim p As XYZ = line.GetEndPoint(0)
        Dim q As XYZ = line.GetEndPoint(1)
        Dim norm As XYZ
        If p.X = q.X Then
            norm = XYZ.BasisX
        ElseIf p.Y = q.Y Then
            norm = XYZ.BasisY
        Else
            norm = XYZ.BasisZ
        End If
#If CONFIG >= "2018" Then
        Dim plane As Plane = Plane.CreateByNormalAndOrigin(norm, p)
#Else
        Dim plane As Plane = creApp.NewPlane(norm, p)
#End If

        'return _credoc.NewSketchPlane( plane ); // 2013

        Return SketchPlane.Create(_doc, plane)
        ' 2014
    End Function

    Public Sub CreateModelLine(p As XYZ, q As XYZ)
        If p.IsAlmostEqualTo(q) Then
            Throw New ArgumentException("Expected two different points.")
        End If
        Dim line__1 As Line = Line.CreateBound(p, q)
        If line__1 Is Nothing Then
            Throw New Exception("Geometry line creation failed.")
        End If
        _credoc.NewModelCurve(line__1, NewSketchPlanePassLine(line__1))
    End Sub

    ''' <summary>
    ''' Return a new sketch plane containing the given curve.
    ''' Update, later: please note that the Revit API provides
    ''' an overload of the NewPlane method taking a CurveArray
    ''' argument, which could presumably be used instead.
    ''' </summary>
    Private Function NewSketchPlaneContainCurve(curve As Curve) As SketchPlane
        Dim p As XYZ = curve.GetEndPoint(0)
        Dim normal As XYZ = GetCurveNormal(curve)
#If CONFIG >= "2018" Then
        Dim plane As Plane = Plane.CreateByNormalAndOrigin(normal, p)
#Else
        Dim plane As Plane = creApp.NewPlane(norm, p)
#End If

#If DEBUG Then
			If Not (TypeOf curve Is Line) Then
				Dim a As CurveArray = _creapp.NewCurveArray()
				a.Append(curve)
				Dim plane2 As Plane = _creapp.NewPlane(a)

				Debug.Assert(Util.IsParallel(plane2.Normal, plane.Normal), "expected equal planes")

				Debug.Assert(Util.IsZero(plane2.SignedDistanceTo(plane.Origin)), "expected equal planes")
			End If
#End If

        'return _credoc.NewSketchPlane( plane ); // 2013

        Return SketchPlane.Create(_doc, plane)
        ' 2014
    End Function

    Public Sub CreateModelCurve(curve As Curve)
        _credoc.NewModelCurve(curve, NewSketchPlaneContainCurve(curve))
    End Sub

    Public Sub DrawPolygons(loops As List(Of List(Of XYZ)))
        Dim p1 As XYZ = XYZ.Zero
        Dim q As XYZ = XYZ.Zero
        Dim first As Boolean
        For Each [loop] As List(Of XYZ) In loops
            first = True
            For Each p As XYZ In [loop]
                If first Then
                    p1 = p
                    first = False
                Else
                    CreateModelLine(p, q)
                End If
                q = p
            Next
            CreateModelLine(q, p1)
        Next
    End Sub

    Public Sub DrawFaceTriangleNormals(f As Face)
        Dim mesh As Mesh = f.Triangulate()
        Dim n As Integer = mesh.NumTriangles

        Dim s As String = "{0} face triangulation returns " & "mesh triangle{1} and normal vector{1}:"

        Debug.Print(s, n, Util.PluralSuffix(n))

        For i As Integer = 0 To n - 1
            Dim t As MeshTriangle = mesh.Triangle(i)

            Dim p As XYZ = (t.Vertex(0) + t.Vertex(1) + t.Vertex(2)) / 3

            Dim v As XYZ = t.Vertex(1) - t.Vertex(0)

            Dim w As XYZ = t.Vertex(2) - t.Vertex(0)

            Dim normal As XYZ = v.CrossProduct(w).Normalize()

            Debug.Print("{0} {1} --> {2}", i, Util.PointString(p), Util.PointString(normal))

            CreateModelLine(p, p + normal)
        Next
    End Sub
End Class