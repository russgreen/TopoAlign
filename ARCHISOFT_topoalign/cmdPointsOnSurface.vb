#Region "Imported Namespaces"
Imports System
Imports System.Collections.Generic
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.Attributes
Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports Autodesk.Revit.UI.Selection
Imports System.Windows.Forms
#End Region

#If CONFIG = "2016" Or CONFIG = "2017" Then
Imports System.Threading.Tasks
Imports System.Net 'for HttpStatusCode 

'Added for REST API
Imports RestSharp
Imports RestSharp.Deserializers
#End If


<Transaction(TransactionMode.Manual)>
Public Class cmdPointsOnSurface
    Implements IExternalCommand

    Dim uiapp As UIApplication
    Dim uidoc As UIDocument
    Dim app As Autodesk.Revit.ApplicationServices.Application
    Dim doc As Document
    Dim sel As Selection

    Dim clsUtil As New Util

    Dim m_Element As Element
    Dim m_Edge As Edge
    Dim m_TopoSurface As Autodesk.Revit.DB.Architecture.TopographySurface

    Dim v3d As View3D

    Public cSettings As Settings

    Public Function Execute(
  ByVal commandData As ExternalCommandData,
  ByRef message As String,
  ByVal elements As ElementSet) _
As Result Implements IExternalCommand.Execute
        cSettings = New Settings
        cSettings.LoadSettings()

        uiapp = commandData.Application
        uidoc = uiapp.ActiveUIDocument
        app = uiapp.Application
        doc = uidoc.Document
        sel = uidoc.Selection

#If CONFIG = "2016" Or CONFIG = "2017" Then
        'check entitlement
        If clsUtil.LicenseCheck(app) = False Then
            Return Result.Failed
        End If
#ElseIf CONFIG = "2016 Trial" Then
        If Date.Now.Month <= 4 And Date.Now.Year = 2016 Then
            'were OK in trial
        Else
            Return Result.Failed
        End If
#End If

        If TypeOf doc.ActiveView Is Autodesk.Revit.DB.View3D Then
            v3d = DirectCast(doc.ActiveView, View3D)
        Else
            TaskDialog.Show("Points on surface", "You must be in a 3D view", TaskDialogCommonButtons.Ok)
            Return Result.Failed
        End If

        Dim fh As New FailureHandler()
        Dim topoFilter As New TopoPickFilter()
        Dim lineFilter As New LinePickFilter()
        Dim elemFilter As New ElemPickFilter()

        Dim points As IList(Of XYZ) = New List(Of XYZ)()
        Dim points1 As IList(Of XYZ) = New List(Of XYZ)()

        'Dim xYZs1 As IList(Of Autodesk.Revit.DB.XYZ) = New List(Of Autodesk.Revit.DB.XYZ)()

        Try
            Dim refToposurface As Reference = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface")
            m_TopoSurface = TryCast(doc.GetElement(refToposurface), Autodesk.Revit.DB.Architecture.TopographySurface)
        Catch ex As Exception
            Return Result.Failed
        End Try

        Dim m_Curves As List(Of Curve)
        m_Curves = New List(Of Curve)()

        Try
            For Each r As Reference In uidoc.Selection.PickObjects(ObjectType.Element, lineFilter, "Select lines(s) to add topo points along")
                Dim m_Curve As Curve

                Dim modelLine As ModelLine = TryCast(doc.GetElement(r), ModelLine)
                Dim modelCurve As ModelCurve = TryCast(doc.GetElement(r), ModelCurve)

                Try
                    m_Curve = modelLine.GeometryCurve
                Catch ex As Exception
                End Try

                Try
                    m_Curve = modelCurve.GeometryCurve
                Catch ex As Exception
                End Try

                m_Curves.Add(m_Curve)
            Next
        Catch ex As Exception
            Return Result.Failed
        End Try

        'sort the curves and make contiguous
        'Dim creapp As Autodesk.Revit.Creation.Application = doc.Application.Create
        Try
            CurveUtils.SortCurvesContiguous(m_Curves)
        Catch ex As Exception
            TaskDialog.Show("Points on surface", "The lines selected must all be connected", TaskDialogCommonButtons.Ok)
            Return Result.Failed
        End Try


        Dim CleanupTopoPoints As Boolean = False
        If IsLoopClosed(m_Curves) = True Then
            If TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes Or TaskDialogCommonButtons.No, TaskDialogResult.Yes) = TaskDialogResult.Yes Then
                CleanupTopoPoints = True
            End If
        End If

        'If = True Then
        '    If TaskDialog.Show("Points on surface", "The lines you selected appear to form a closed loop.  Would you like to remove the topo points within that loop?", TaskDialogCommonButtons.Yes Or TaskDialogCommonButtons.No, TaskDialogResult.Yes) = TaskDialogResult.Ok Then
        '        DeleteTopoPointsWithinCurves(m_Curves, m_TopoSurface)
        '    End If
        'End If

        'If CurvesFormClosedPolygon(m_Curves) = True Then

        'End If

        Try
            Using tes As New Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Align topo")
                tes.Start(m_TopoSurface.Id)

                Dim opt As New Options()
                opt.ComputeReferences = True

                points = GetPointsFromCurvesOnSurface(m_Curves)

                If points.Count = 0 Then
                    TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the lines selected.", TaskDialogCommonButtons.Ok)
                    tes.Cancel()
                    Return Result.Failed
                End If

                'loop through each point and use reference intersector to get topo Z
                For Each pt As XYZ In points
                    ' The 3 inputs for ReferenceIntersector are:
                    ' A filter specifying that only ceilings will be reported
                    ' A FindReferenceTarget option indicating what types of objects to report
                    ' A 3d view (see http://wp.me/p2X0gy-2p for more info on how this works)

                    'Dim intersector As New ReferenceIntersector(New ElementCategoryFilter(BuiltInCategory.OST_TopographySurface), FindReferenceTarget.All, (From v In New FilteredElementCollector(doc).OfClass(GetType(View3D)).Cast(Of View3D)() Where v.IsTemplate = False AndAlso v.IsPerspective = False).First())
                    'Dim intersector As New ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, (From v In New FilteredElementCollector(doc).OfClass(GetType(View3D)).Cast(Of View3D)() Where v.IsTemplate = False AndAlso v.IsPerspective = False).First())
                    'Dim intersector As New ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, v3d)

                    '' FindNearest finds the first item hit by the ray
                    '' XYZ.BasisZ shoots the ray "up"
                    'Dim rwC As ReferenceWithContext = intersector.FindNearest(pt, XYZ.BasisZ)
                    'Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z + rwC.Proximity)

                    'points1.Add(pt1)

                    Dim startPt As Autodesk.Revit.DB.XYZ = pt
                    Dim endPtUp As Autodesk.Revit.DB.XYZ = New XYZ(pt.X, pt.Y, pt.Z + 100)

                    Dim dirUp As Autodesk.Revit.DB.XYZ = (endPtUp - startPt).Normalize()

                    Dim referenceIntersector As New ReferenceIntersector(m_TopoSurface.Id, FindReferenceTarget.All, v3d)

                    Dim obstructionsOnUnboundLineUp As IList(Of ReferenceWithContext) = referenceIntersector.Find(startPt, dirUp)
                    Dim point As XYZ = Nothing
                    Dim gRefWithContext As ReferenceWithContext = obstructionsOnUnboundLineUp.FirstOrDefault()

                    Dim gRef As Reference = gRefWithContext.GetReference()
                    point = gRef.GlobalPoint

                    points1.Add(point)
                Next

                If CleanupTopoPoints = True Then
                    DeleteTopoPointsWithinCurves(m_Curves)
                End If

                'we now have points with correct Z values.
                'delete duplicate points and add to topo
                'TODO: Check duplicates in more robust way.
                'texting 0.1 instead of 0.01
                Dim comparer As New XyzEqualityComparer '(0.1)
                Using t As New Transaction(doc, "add points")
                    t.Start()
                    m_TopoSurface.AddPoints(points1.Distinct(comparer).ToList)
                    t.Commit()
                End Using

                tes.Commit(fh)

            End Using
        Catch ex As Exception
            Return Result.Failed
            Exit Function
        End Try

        Return Result.Succeeded
    End Function

    Private Function GetPointsFromCurvesOnSurface(m_Curves As List(Of Curve)) As IList(Of XYZ)
        Dim points As New List(Of XYZ)
        Dim divide As Double = 1000 * 0.00328084

        For Each m_Curve As Curve In m_Curves
            Dim i As Integer = m_Curve.Tessellate.Count
            If i > 2 Then
                For Each pt As XYZ In m_Curve.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z)
                    points.Add(pt1)
                Next
            Else
                Dim len As Double = m_Curve.ApproximateLength
                If len > divide Then
                    Dim pt0 As New XYZ(m_Curve.Tessellate(0).X, m_Curve.Tessellate(0).Y, m_Curve.Tessellate(0).Z)
                    Dim pt1 As New XYZ(m_Curve.Tessellate(1).X, m_Curve.Tessellate(1).Y, m_Curve.Tessellate(1).Z)

                    For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                        Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z)
                        points.Add(p)
                    Next
                Else
                    For Each pt As XYZ In m_Curve.Tessellate()
                        Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z)
                        points.Add(pt1)
                    Next
                End If
            End If
        Next

        Return points
    End Function

    Private Function IsLoopClosed(m_Curves As List(Of Curve)) As Boolean
        If CurveLoop.Create(m_Curves).IsOpen = True Then
            Return False
        Else
            Return True
        End If
    End Function


    Private Sub DeleteTopoPointsWithinCurves(m_Curves As List(Of Curve))
        Dim polygons As New List(Of List(Of XYZ))

        For Each c As Curve In m_Curves
            Dim polygon As New List(Of XYZ)
            Dim i As Integer = c.Tessellate.Count
            If i > 2 Then
                For Each pt As XYZ In c.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z)
                    polygon.Add(pt1)
                Next
            Else
                For Each pt As XYZ In c.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z)
                    polygon.Add(pt1)
                Next
            End If
            polygons.Add(polygon)
        Next

        Dim flat_polygons As List(Of List(Of UV)) = Util.Flatten(polygons)


        Dim xyzs As New List(Of XYZ)
        For Each polygon As List(Of XYZ) In polygons
            For Each pt As XYZ In polygon
                xyzs.Add(pt)
            Next
        Next
        Dim uvArr As New UVArray(xyzs)

        Dim poly As New List(Of UV)
        For Each polygon As List(Of UV) In flat_polygons
            For Each pt As UV In polygon
                poly.Add(pt)
            Next
        Next

        'bounding box of curves and topo for elevation
        Dim bb As New JtBoundingBoxXyz()
        bb = JtBoundingBoxXyz.GetBoundingBoxOf(polygons)

        Dim v As Autodesk.Revit.DB.View = Nothing
        Dim min As New XYZ(bb.Min.X, bb.Min.Y, m_TopoSurface.BoundingBox(v).Min.Z)
        Dim max As New XYZ(bb.Max.X, bb.Max.Y, m_TopoSurface.BoundingBox(v).Max.Z)

        'Get topopoints withing bounding box 
        Dim outline As New Outline(min, max)
        Dim points As New List(Of XYZ)

        Dim pts As New List(Of XYZ)
        pts = TryCast(m_TopoSurface.GetInteriorPoints, List(Of XYZ))

        For Each pt As XYZ In pts
            If outline.Contains(pt, 0.000000001) Then
                points.Add(pt)
            End If
        Next

        'Check each point to see if point is with 2D boundary
        Dim points1 As New List(Of XYZ)
        Using pf As New ProgressForm("Analyzing topo points.", "{0} points of " & points.Count & " processed...", points.Count)
            For Each pt As XYZ In points
                'If PointInPoly.PolygonContains(poly, Util.Flatten(pt)) = True Then
                'If PointInPoly.PolygonContains(uvArr, Util.Flatten(pt)) = True Then
                If PointInPoly.PointInPolygon(uvArr, Util.Flatten(pt)) = True Then
                    points1.Add(pt)
                End If
                pf.Increment()
            Next
        End Using

        'Remove topo points if answer is true
        If points1.Count > 0 Then
            Using t As New Transaction(doc, "removing points")
                t.Start()
                m_TopoSurface.DeletePoints(points1)
                t.Commit()
            End Using
        End If


    End Sub

End Class
