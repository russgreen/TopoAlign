Imports System.Collections.Generic
Imports System.Linq
Imports Autodesk.Revit.DB
Imports System.Diagnostics

NotInheritable Class CurveGetEnpointExtension
    Private Sub New()
    End Sub

    Public Shared Function GetEndPoint(curve As Curve, i As Integer) As XYZ
        Return curve.GetEndPoint(i)
    End Function
End Class


''' <summary>
''' Curve loop utilities supporting resorting and 
''' orientation of curves to form a contiguous 
''' closed loop.
''' </summary>
Class CurveUtils
    Const _inch As Double = 1.0 / 12.0
    Const _sixteenth As Double = _inch / 16.0

    Public Enum FailureCondition
        Success
        CurvesNotContigous
        CurveLoopAboveTarget
        NoIntersection
    End Enum

    ''' <summary>
    ''' Predicate to report whether the given curve 
    ''' type is supported by this utility class.
    ''' </summary>
    ''' <param name="curve">The curve.</param>
    ''' <returns>True if the curve type is supported, 
    ''' false otherwise.</returns>
    Public Shared Function IsSupported(curve As Curve) As Boolean
        Return TypeOf curve Is Line OrElse TypeOf curve Is Arc
    End Function

    ''' <summary>
    ''' Create a new curve with the same 
    ''' geometry in the reverse direction.
    ''' </summary>
    ''' <param name="orig">The original curve.</param>
    ''' <returns>The reversed curve.</returns>
    ''' <throws cref="NotImplementedException">If the 
    ''' curve type is not supported by this utility.</throws>
    Private Shared Function CreateReversedCurve(creapp As Autodesk.Revit.Creation.Application, orig As Curve) As Curve
        If Not IsSupported(orig) Then
            Throw New NotImplementedException("CreateReversedCurve for type " + orig.[GetType]().Name)
        End If

        If TypeOf orig Is Line Then
            Return Line.CreateBound(orig.GetEndPoint(1), orig.GetEndPoint(0))
            'Return creapp.NewLineBound(orig.GetEndPoint(1), orig.GetEndPoint(0))
        ElseIf TypeOf orig Is Arc Then
            Return Arc.Create(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5, True))
            'Return creapp.NewArc(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5, True))
        Else
            Throw New Exception("CreateReversedCurve - Unreachable")
        End If
    End Function
    Private Shared Function CreateReversedCurve(orig As Curve) As Curve
        If Not IsSupported(orig) Then
            Throw New NotImplementedException("CreateReversedCurve for type " + orig.[GetType]().Name)
        End If

        If TypeOf orig Is Line Then
            Return Line.CreateBound(orig.GetEndPoint(1), orig.GetEndPoint(0))
        ElseIf TypeOf orig Is Arc Then
            Return Arc.Create(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5, True))
        Else
            Throw New Exception("CreateReversedCurve - Unreachable")
        End If
    End Function

    ''' <summary>
    ''' Sort a list of curves to make them correctly 
    ''' ordered and oriented to form a closed loop.
    ''' </summary>
    Public Shared Sub SortCurvesContiguous(creapp As Autodesk.Revit.Creation.Application, curves As IList(Of Curve), debug_output As Boolean)
        Dim n As Integer = curves.Count

        ' Walk through each curve (after the first) 
        ' to match up the curves in order

        For i As Integer = 0 To n - 1
            Dim curve As Curve = curves(i)
            Dim endPoint As XYZ = curve.GetEndPoint(1)

            If debug_output Then
                Debug.Print("{0} endPoint {1}", i, Util.PointString(endPoint))
            End If

            Dim p As XYZ

            ' Find curve with start point = end point

            Dim found As Boolean = (i + 1 >= n)

            For j As Integer = i + 1 To n - 1
                p = curves(j).GetEndPoint(0)

                ' If there is a match end->start, 
                ' this is the next curve

                If _sixteenth > p.DistanceTo(endPoint) Then
                    If debug_output Then
                        Debug.Print("{0} start point, swap with {1}", j, i + 1)
                    End If

                    If i + 1 <> j Then
                        Dim tmp As Curve = curves(i + 1)
                        curves(i + 1) = curves(j)
                        curves(j) = tmp
                    End If
                    found = True
                    Exit For
                End If

                p = curves(j).GetEndPoint(1)

                ' If there is a match end->end, 
                ' reverse the next curve

                If _sixteenth > p.DistanceTo(endPoint) Then
                    If i + 1 = j Then
                        If debug_output Then
                            Debug.Print("{0} end point, reverse {1}", j, i + 1)
                        End If

                        curves(i + 1) = CreateReversedCurve(creapp, curves(j))
                    Else
                        If debug_output Then
                            Debug.Print("{0} end point, swap with reverse {1}", j, i + 1)
                        End If

                        Dim tmp As Curve = curves(i + 1)
                        curves(i + 1) = CreateReversedCurve(creapp, curves(j))
                        curves(j) = tmp
                    End If
                    found = True
                    Exit For
                End If
            Next

            If Not found Then
                Throw New Exception("SortCurvesContiguous:" + " non-contiguous input curves")
            End If
        Next
    End Sub

    Public Shared Sub SortCurvesContiguous(curves As IList(Of Curve))
        Dim _precision1 As Double = 1.0 / 12.0 / 16.0
        ' around 0.00520833
        Dim _precision2 As Double = 0.001
        ' limit for CurveLoop.Create(...)
        Dim n As Integer = curves.Count

        ' Walk through each curve (after the first)
        ' to match up the curves in order

        For i As Integer = 0 To n - 1
            Dim curve As Curve = curves(i)

            Dim beginPoint As XYZ = curve.GetEndPoint(0)
            Dim endPoint As XYZ = curve.GetEndPoint(1)

            Dim p As XYZ, q As XYZ

            ' Find curve with start point = end point

            Dim found As Boolean = (i + 1 >= n)

            For j As Integer = i + 1 To n - 1
                p = curves(j).GetEndPoint(0)
                q = curves(j).GetEndPoint(1)

                ' If there is a match end->start,
                ' this is the next curve
                If p.DistanceTo(endPoint) < _precision1 Then
                    If p.DistanceTo(endPoint) > _precision2 Then
                        Dim intermediate As New XYZ((endPoint.X + p.X) / 2.0, (endPoint.Y + p.Y) / 2.0, (endPoint.Z + p.Z) / 2.0)

                        curves(i) = Line.CreateBound(beginPoint, intermediate)

                        curves(j) = Line.CreateBound(intermediate, q)
                    End If

                    If i + 1 <> j Then
                        Dim tmp As Curve = curves(i + 1)
                        curves(i + 1) = curves(j)
                        curves(j) = tmp
                    End If
                    found = True
                    Exit For
                End If

                ' If there is a match end->end,
                ' reverse the next curve

                If q.DistanceTo(endPoint) < _precision1 Then
                    If q.DistanceTo(endPoint) > _precision2 Then
                        Dim intermediate As New XYZ((endPoint.X + q.X) / 2.0, (endPoint.Y + q.Y) / 2.0, (endPoint.Z + q.Z) / 2.0)

                        curves(i) = Line.CreateBound(beginPoint, intermediate)

                        curves(j) = Line.CreateBound(p, intermediate)
                    End If

                    If i + 1 = j Then
                        curves(i + 1) = CreateReversedCurve(curves(j))
                    Else
                        Dim tmp As Curve = curves(i + 1)
                        curves(i + 1) = CreateReversedCurve(curves(j))
                        curves(j) = tmp
                    End If
                    found = True
                    Exit For
                End If
            Next

            If Not found Then
                Throw New Exception("SortCurvesContiguous :" + " non-contiguous input curves")
            End If
        Next
    End Sub

    ''' <summary>
    ''' Return a list of curves which are correctly 
    ''' ordered and oriented to form a closed loop.
    ''' </summary>
    ''' <param name="doc">The document.</param>
    ''' <param name="boundaries">The list of curve element references which are the boundaries.</param>
    ''' <returns>The list of curves.</returns>
    Public Shared Function GetContiguousCurvesFromSelectedCurveElements(doc As Document, boundaries As IList(Of Reference), debug_output As Boolean) As IList(Of Curve)
        Dim curves As New List(Of Curve)()

        ' Build a list of curves from the curve elements

        For Each reference As Reference In boundaries
            Dim curveElement As CurveElement = TryCast(doc.GetElement(reference), CurveElement)

            curves.Add(curveElement.GeometryCurve.Clone())
        Next

        SortCurvesContiguous(doc.Application.Create, curves, debug_output)

        Return curves
    End Function

    ''' <summary>
    ''' Identifies if the curve lies entirely in an XY plane (Z = constant)
    ''' </summary>
    ''' <param name="curve">The curve.</param>
    ''' <returns>True if the curve lies in an XY plane, false otherwise.</returns>
    Public Shared Function IsCurveInXYPlane(curve As Curve) As Boolean
        ' quick reject - are endpoints at same Z

        Dim zDelta As Double = curve.GetEndPoint(1).Z - curve.GetEndPoint(0).Z

        If Math.Abs(zDelta) > 0.00001 Then
            Return False
        End If

        If Not (TypeOf curve Is Line) AndAlso Not curve.IsCyclic Then
            ' Create curve loop from curve and 
            ' connecting line to get plane

            Dim curves As New List(Of Curve)()
            curves.Add(curve)

            'curves.Add(Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0)));

            Dim curveLoop__1 As CurveLoop = CurveLoop.Create(curves)

            Dim normal As XYZ = curveLoop__1.GetPlane().Normal.Normalize()

            If Not normal.IsAlmostEqualTo(XYZ.BasisZ) AndAlso Not normal.IsAlmostEqualTo(XYZ.BasisZ.Negate()) Then
                Return False
            End If
        End If
        Return True
    End Function
End Class
