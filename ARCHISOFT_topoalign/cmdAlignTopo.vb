#Region "Imported Namespaces"
Imports System
Imports System.Collections.Generic
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.Attributes
Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports Autodesk.Revit.UI.Selection
Imports System.Windows.Forms
Imports Microsoft.AppCenter
Imports Microsoft.AppCenter.Analytics
Imports Microsoft.AppCenter.Crashes
#End Region

#If CONFIG = "2016" Or CONFIG = "2017" Then
Imports System.Threading.Tasks
Imports System.Net 'for HttpStatusCode 

'Added for REST API
Imports RestSharp
Imports RestSharp.Deserializers
#End If


<Transaction(TransactionMode.Manual)>
Public Class cmdAlignTopo
    Implements IExternalCommand

    Dim uiapp As UIApplication
    Dim uidoc As UIDocument
    Dim app As Autodesk.Revit.ApplicationServices.Application
    Dim doc As Document
    Dim sel As Selection

    Dim offset As Decimal
    Dim divide As Decimal

    Dim clsUtil As New Util

    Dim m_Element As Element
    Dim m_Edge As Edge
    Dim m_TopoSurface As Autodesk.Revit.DB.Architecture.TopographySurface

    Dim m_DocUnits As Units
#If CONFIG < "2021" Then
    Dim m_DocDisplayUnits As DisplayUnitType
    Dim m_UseDisplayUnits As DisplayUnitType
#Else
    Dim m_DocDisplayUnits As ForgeTypeId
    Dim m_UseDisplayUnits As ForgeTypeId
#End If

    Public cSettings As Settings

    ''' <summary>
    ''' The one and only method required by the IExternalCommand interface, the main entry point for every external command.
    ''' </summary>
    ''' <param name="commandData">Input argument providing access to the Revit application, its documents and their properties.</param>
    ''' <param name="message">Return argument to display a message to the user in case of error if Result is not Succeeded.</param>
    ''' <param name="elements">Return argument to highlight elements on the graphics screen if Result is not Succeeded.</param>
    ''' <returns>Cancelled, Failed or Succeeded Result code.</returns>
    Public Function Execute(
      ByVal commandData As ExternalCommandData,
      ByRef message As String,
      ByVal elements As ElementSet) _
    As Result Implements IExternalCommand.Execute

        'AppCenter.Start("c26c8f38-0aad-44c7-9064-478429495727", GetType(Analytics), GetType(Crashes))
        'AppCenter.LogLevel = LogLevel.Verbose

        Dim revitVersion As String
#If CONFIG = "2018" Then
            revitVersion = "2018"
#ElseIf CONFIG = "2019" Then
            revitVersion = "2019"
#ElseIf CONFIG = "2020" Then
            revitVersion = "2020"
#ElseIf CONFIG = "2021" Then
        revitVersion = "2021"
#ElseIf CONFIG = "2022" Then
            revitVersion = "2022"
#End If

        'Analytics.TrackEvent($"Revit Version {revitVersion}")

        cSettings = New Settings
        cSettings.LoadSettings()

        uiapp = commandData.Application
        uidoc = uiapp.ActiveUIDocument
        app = uiapp.Application
        doc = uidoc.Document
        sel = uidoc.Selection

#If CONFIG < "2021" Then
        m_DocUnits = doc.GetUnits
        m_DocDisplayUnits = doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits
#Else
        m_DocUnits = doc.GetUnits
        m_DocDisplayUnits = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId
#End If


        Dim frm As New frmAlignTopo

        With frm
            .rdoElem.Checked = cSettings.SingleElement
            .rdoEdge.Checked = Not (.rdoElem.Checked)

            divide = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits(cSettings.DivideEdgeDistance, m_DocDisplayUnits))
            offset = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits(cSettings.VerticalOffset, m_DocDisplayUnits))

            If divide > .nudDivide.Maximum Then
                .nudDivide.Value = .nudDivide.Maximum
            Else
                .nudDivide.Value = divide
            End If

            If offset > .nudVertOffset.Maximum Then
                .nudVertOffset.Value = .nudVertOffset.Maximum
            Else
                .nudVertOffset.Value = offset
            End If


            .chkRemoveInside.Checked = cSettings.CleanTopoPoints
            .rdoTop.Checked = cSettings.TopFace
            .rdoBottom.Checked = Not (.rdoTop.Checked)
            '.lblUnits.Text = "Units are in " & LabelUtils.GetLabelFor(m_DocDisplayUnits)

#If CONFIG < "2021" Then
            'pupulate the display units dropdown
            For Each displayUnitType As DisplayUnitType In UnitUtils.GetValidDisplayUnits(UnitType.UT_Length)
                'If LabelUtils.GetLabelFor(displayUnitType).ToLower.Contains("fractional") Then
                '    'don't add it
                'Else
                '    .DisplayUnitTypecomboBox.Items.AddRange(New Object() {displayUnitType})
                '    .DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelFor(displayUnitType))
                'End If
                .DisplayUnitTypecomboBox.Items.AddRange(New Object() {displayUnitType})
                .DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelFor(displayUnitType))
            Next

            .DisplayUnitTypecomboBox.SelectedItem = m_DocDisplayUnits
            .DisplayUnitcomboBox.SelectedIndex = .DisplayUnitTypecomboBox.SelectedIndex
#Else
            'pupulate the display units dropdown
            For Each displayUnitType As ForgeTypeId In UnitUtils.GetValidUnits(SpecTypeId.Length)
                .DisplayUnitTypecomboBox.Items.AddRange(New Object() {displayUnitType})
                Debug.WriteLine(LabelUtils.GetLabelForUnit(displayUnitType))
                .DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelForUnit(displayUnitType))
            Next

            .DisplayUnitTypecomboBox.SelectedItem = m_DocDisplayUnits
            .DisplayUnitcomboBox.SelectedIndex = .DisplayUnitTypecomboBox.SelectedIndex
#End If

            If .ShowDialog = DialogResult.OK Then
#If CONFIG < "2021" Then
                m_UseDisplayUnits = DirectCast(.DisplayUnitTypecomboBox.SelectedItem, DisplayUnitType)
                divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(.nudDivide.Value, m_UseDisplayUnits))
                offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(.nudVertOffset.Value, m_UseDisplayUnits))
#Else
                m_UseDisplayUnits = DirectCast(.DisplayUnitTypecomboBox.SelectedItem, ForgeTypeId)
                divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(.nudDivide.Value, m_UseDisplayUnits))
                offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(.nudVertOffset.Value, m_UseDisplayUnits))
#End If

                'first save the settings for next time
                cSettings.SingleElement = .rdoElem.Checked
                cSettings.DivideEdgeDistance = divide
                cSettings.VerticalOffset = offset
                cSettings.CleanTopoPoints = .chkRemoveInside.Checked
                cSettings.TopFace = .rdoTop.Checked
                cSettings.SaveSettings()

                If AlignTopo(.rdoTop.Checked, .rdoEdge.Checked) = False Then
                    Return Result.Failed
                    Exit Function
                End If
            End If
        End With

        Return Result.Succeeded
    End Function

    Private Function AlignTopo(Optional TopFace As Boolean = True, Optional UseEdge As Boolean = False) As Boolean
        Dim retval As Boolean = True

        Dim fh As New FailureHandler()
        Dim topoFilter As New TopoPickFilter()
        Dim elemFilter As New ElemPickFilter()

        Dim points As IList(Of XYZ) = New List(Of XYZ)()
        Dim points1 As IList(Of XYZ) = New List(Of XYZ)()

        Dim xYZs1 As IList(Of Autodesk.Revit.DB.XYZ) = New List(Of Autodesk.Revit.DB.XYZ)()

        Try
            Dim refToposurface As Reference = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface")
            m_TopoSurface = TryCast(doc.GetElement(refToposurface), Autodesk.Revit.DB.Architecture.TopographySurface)
        Catch ex As Exception
            Return False
        End Try


        'Dim m_Elements As List(Of Element)
        Dim m_Edges As List(Of Edge)
        Dim m_Curves As List(Of Curve)

        If UseEdge = False Then
            Try
                Dim refElement As Reference = uidoc.Selection.PickObject(ObjectType.Element, elemFilter, "Select an object to align to")
                m_Element = doc.GetElement(refElement)
            Catch ex As Exception
                Return False
            End Try
        Else
            Try
                'we're just picking edges
                m_Edges = New List(Of Edge)()
                m_Curves = New List(Of Curve)()

                For Each r As Reference In uidoc.Selection.PickObjects(ObjectType.Edge, "Select edge(s) to align to")

                    Dim m_Element As Element = doc.GetElement(r.ElementId)
                    Dim m_SelectedEdge As Edge = TryCast(m_Element.GetGeometryObjectFromReference(r), Edge)
                    Dim m_Curve As Curve = m_SelectedEdge.AsCurve

                    Dim fi As FamilyInstance = TryCast(m_Element, FamilyInstance)

                    If fi IsNot Nothing Then
                        Dim the_list_of_the_joined As ICollection(Of ElementId) = JoinGeometryUtils.GetJoinedElements(doc, fi)
                        If the_list_of_the_joined.Count = 0 Then
                            m_Curve = m_Curve.CreateTransformed(fi.GetTransform)
                        End If
                    End If

                    m_Curves.Add(m_Curve)
                Next
            Catch ex As Exception
                Return False
            End Try
        End If

        Try
            Using tes As New Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Align topo")
                tes.Start(m_TopoSurface.Id)

                Dim fi As FamilyInstance = TryCast(m_Element, FamilyInstance)
                Dim opt As New Options()
                opt.ComputeReferences = True

                If UseEdge = False Then

                    If fi IsNot Nothing Then
                        points = GetPointsFromFamily(fi.Geometry(opt), TopFace)
                    Else
                        If cSettings.CleanTopoPoints = True Then CleanupTopoPoints(m_Element)

                        points = GetPointsFromElement(m_Element, TopFace)
                    End If

                    If points.Count = 0 Then
                        TaskDialog.Show("Topo Align", "Unable to get a suitable list of points from the object. Try picking edges", TaskDialogCommonButtons.Ok)
                        tes.Cancel()
                        Return False
                    End If
                Else
                    points = GetPointsFromCurves(m_Curves)
                End If

                'delete duplicate points
                Dim comparer As New XyzEqualityComparer '(0.01)
                Using t As New Transaction(doc, "add points")
                    t.Start()
                    m_TopoSurface.AddPoints(points.Distinct(comparer).ToList)
                    t.Commit()
                End Using

                tes.Commit(fh)
            End Using


        Catch ex As Exception
            TaskDialog.Show("Align topo", ex.Message)
            'Crashes.TrackError(ex)
            Return False
        End Try

        Return retval

    End Function

    Private Function GetPointsFromElement(e As Element, TopFace As Boolean) As List(Of XYZ)
        Dim points As New List(Of XYZ)

        Dim opt As New Options()
        opt.ComputeReferences = True

        Dim m_GeometryElement As GeometryElement = e.Geometry(opt)

        For Each m_GeometryObject As GeometryObject In m_GeometryElement
            Dim m_Solid As Solid = TryCast(m_GeometryObject, Solid)
            Dim m_Faces As New List(Of Face)

            If m_Solid = Nothing Then

            Else
                For Each f As Face In m_Solid.Faces
                    If TopFace = True Then
                        If Util.IsTopFace(f) = True Then
                            m_Faces.Add(f)
                        End If
                    Else
                        If Util.IsBottomFace(f) = True Then
                            m_Faces.Add(f)
                        End If
                    End If
                Next
            End If

            For Each f As Face In m_Faces
                For Each ea As EdgeArray In f.EdgeLoops
                    For Each m_edge As Edge In ea
                        Dim i As Integer = m_edge.Tessellate.Count
                        If i > 2 Then
                            For Each pt As XYZ In m_edge.Tessellate()
                                Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                                points.Add(pt1)
                            Next
                        Else
                            Dim len As Double = m_edge.ApproximateLength
                            If len > divide Then
                                Dim pt0 As New XYZ(m_edge.Tessellate(0).X, m_edge.Tessellate(0).Y, m_edge.Tessellate(0).Z)
                                Dim pt1 As New XYZ(m_edge.Tessellate(1).X, m_edge.Tessellate(1).Y, m_edge.Tessellate(1).Z)

                                For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                                    Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                                    points.Add(p)
                                Next
                            Else
                                For Each pt As XYZ In m_edge.Tessellate()
                                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                                    points.Add(pt1)
                                Next
                            End If
                        End If
                    Next
                Next
            Next
        Next

        Return points
    End Function

    Private Function GetPointsFromFamily(e As GeometryElement, TopFace As Boolean) As List(Of XYZ)
        Dim points As New List(Of XYZ)

        'we have selected a family instance so try and get the geometry from it
        Dim m_Face As PlanarFace = Nothing
        Dim m_Faces As New List(Of Face)

        ''force the bottom edge to be selected
        'TopFace = False

        For Each m_GeometryObject As GeometryObject In e
            Dim m_GeometryInstance As GeometryInstance = TryCast(m_GeometryObject, GeometryInstance)
            Dim m_GeometryInstanceElement As GeometryElement = m_GeometryInstance.GetInstanceGeometry

            For Each m_GeometryInstanceObject As GeometryObject In m_GeometryInstanceElement
                Dim m_Solid As Solid = TryCast(m_GeometryInstanceObject, Solid)
                If m_Solid = Nothing Then
                    Return points
                Else
                    For Each f As Face In m_Solid.Faces
                        If TopFace = True Then
                            If Util.IsTopFace(f) = True Then
                                m_Faces.Add(f)
                            End If
                        Else
                            If Util.IsBottomFace(f) = True Then
                                m_Faces.Add(f)
                            End If
                        End If
                    Next

                    For Each f As Face In m_Faces
                        Dim pf As PlanarFace = TryCast(f, PlanarFace)

                        If pf IsNot Nothing Then
                            If m_Face = Nothing Then m_Face = pf
                            If pf.Origin.Z < m_Face.Origin.Z Then
                                m_Face = pf
                            End If
                        End If
                    Next

                    If cSettings.CleanTopoPoints = True Then CleanupTopoPoints(m_Solid)
                End If
            Next
        Next

        'For Each lf As Face In m_LowestFaces
        For Each ea As EdgeArray In m_Face.EdgeLoops
            'For Each ea As EdgeArray In lf.EdgeLoops
            For Each m_edge As Edge In ea
                Dim i As Integer = m_edge.Tessellate.Count
                If i > 2 Then
                    For Each pt As XYZ In m_edge.Tessellate()
                        Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                        points.Add(pt1)
                    Next
                Else
                    Dim len As Double = m_edge.ApproximateLength
                    If len > divide Then
                        Dim pt0 As New XYZ(m_edge.Tessellate(0).X, m_edge.Tessellate(0).Y, m_edge.Tessellate(0).Z)
                        Dim pt1 As New XYZ(m_edge.Tessellate(1).X, m_edge.Tessellate(1).Y, m_edge.Tessellate(1).Z)

                        For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                            Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                            points.Add(p)
                        Next
                    Else
                        For Each pt As XYZ In m_edge.Tessellate()
                            Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                            points.Add(pt1)
                        Next
                    End If
                End If
            Next 'm_edge
        Next 'ea
        'Next 'lf

        Return points


    End Function

    Private Function GetPointsFromEdge(m_Edge As Edge) As IList(Of XYZ)
        Dim points As New List(Of XYZ)

        Dim i As Integer = m_Edge.Tessellate.Count
        If i > 2 Then
            For Each pt As XYZ In m_Edge.Tessellate()
                Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                points.Add(pt1)
            Next
        Else
            Dim len As Double = m_Edge.ApproximateLength
            If len > divide Then
                Dim pt0 As New XYZ(m_Edge.Tessellate(0).X, m_Edge.Tessellate(0).Y, m_Edge.Tessellate(0).Z)
                Dim pt1 As New XYZ(m_Edge.Tessellate(1).X, m_Edge.Tessellate(1).Y, m_Edge.Tessellate(1).Z)

                For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                    Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                    points.Add(p)
                Next
            Else
                For Each pt As XYZ In m_Edge.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                    points.Add(pt1)
                Next
            End If
        End If

        Return points
    End Function

    Private Function GetPointsFromEdges(m_Edges As IList(Of Edge)) As IList(Of XYZ)
        Dim points As New List(Of XYZ)

        For Each m_Edge As Edge In m_Edges
            Dim i As Integer = m_Edge.Tessellate.Count
            If i > 2 Then
                For Each pt As XYZ In m_Edge.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                    points.Add(pt1)
                Next
            Else
                Dim len As Double = m_Edge.ApproximateLength
                If len > divide Then
                    Dim pt0 As New XYZ(m_Edge.Tessellate(0).X, m_Edge.Tessellate(0).Y, m_Edge.Tessellate(0).Z)
                    Dim pt1 As New XYZ(m_Edge.Tessellate(1).X, m_Edge.Tessellate(1).Y, m_Edge.Tessellate(1).Z)

                    For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                        Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                        points.Add(p)
                    Next
                Else
                    For Each pt As XYZ In m_Edge.Tessellate()
                        Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                        points.Add(pt1)
                    Next
                End If
            End If
        Next

        Return points
    End Function

    Private Function GetPointsFromCurves(m_Curves As List(Of Curve)) As IList(Of XYZ)
        Dim points As New List(Of XYZ)

        For Each m_Curve As Curve In m_Curves
            Dim i As Integer = m_Curve.Tessellate.Count
            If i > 2 Then
                For Each pt As XYZ In m_Curve.Tessellate()
                    Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                    points.Add(pt1)
                Next
            Else
                Dim len As Double = m_Curve.ApproximateLength
                If len > divide Then
                    Dim pt0 As New XYZ(m_Curve.Tessellate(0).X, m_Curve.Tessellate(0).Y, m_Curve.Tessellate(0).Z)
                    Dim pt1 As New XYZ(m_Curve.Tessellate(1).X, m_Curve.Tessellate(1).Y, m_Curve.Tessellate(1).Z)

                    For Each pt As XYZ In Util.DividePoints(pt0, pt1, len, divide)
                        Dim p As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                        points.Add(p)
                    Next
                Else
                    For Each pt As XYZ In m_Curve.Tessellate()
                        Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                        points.Add(pt1)
                    Next
                End If
            End If
        Next

        Return points
    End Function

    Private Sub CleanupTopoPoints(s As Solid)

    End Sub

    Private Sub CleanupTopoPoints(elem As Element)
        'try and get boundary and cleanup topo
        Select Case elem.Category.Name
            Case Is = "Floors"
            Case Is = "Roofs"
            Case Is = "Walls"
            Case Is = "Pads"
            Case Else
                'don't cleanup
                Exit Sub
        End Select


        'Dim elems As New List(Of Element)()
        'elems.Add(elem)

        'Dim opt As Options = app.Create.NewGeometryOptions()
        'Dim polygons As List(Of List(Of XYZ)) = GetFloorBoundaryPolygons(elems, opt)

        'Dim creator As New Creator(doc)

        'Using t As New Transaction(doc)
        '    t.Start("Draw Slab Boundaries")
        '    creator.DrawPolygons(polygons)
        '    t.Commit()
        'End Using

        Dim polygons As List(Of List(Of XYZ)) = New List(Of List(Of XYZ))()
        Dim opt As New Options()
        opt.ComputeReferences = True

        Dim m_GeometryElement As GeometryElement = elem.Geometry(opt)
        For Each m_GeometryObject As GeometryObject In m_GeometryElement
            Dim m_Solid As Solid = TryCast(m_GeometryObject, Solid)
            Dim m_Faces As New List(Of Face)
            If m_Solid = Nothing Then
            Else
                For Each f As Face In m_Solid.Faces
                    If Util.IsBottomFace(f) = True Then
                        m_Faces.Add(f)
                    End If
                Next
            End If

            For Each f As Face In m_Faces
                Dim polygon As List(Of XYZ) = New List(Of XYZ)()

                For Each ea As EdgeArray In f.EdgeLoops
                    For Each m_edge As Edge In ea
                        Dim i As Integer = m_edge.Tessellate.Count
                        If i > 2 Then
                            For Each pt As XYZ In m_edge.Tessellate()
                                Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                                polygon.Add(pt1)
                            Next
                        Else
                            For Each pt As XYZ In m_edge.Tessellate()
                                Dim pt1 As XYZ = New XYZ(pt.X, pt.Y, pt.Z - offset)
                                polygon.Add(pt1)
                            Next
                        End If
                    Next

                    polygons.Add(polygon)
                Next
            Next
        Next

        ''Get 2D boundary of solid
        'Dim m_Faces As New List(Of Face)()
        'Dim opt As New Options()
        'opt.ComputeReferences = True
        'Dim m_elems As New List(Of Element)
        'm_elems.Add(elem)

        'Dim polygons As List(Of List(Of XYZ)) = Util.GetBoundaryPolygons(m_elems, opt)
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

        'Get topopoints withing bounding box of element
        Dim v As Autodesk.Revit.DB.View = Nothing
        Dim bb As BoundingBoxXYZ = elem.BoundingBox(v)
        Dim min As New XYZ(bb.Min.X - 1, bb.Min.Y - 1, m_TopoSurface.BoundingBox(v).Min.Z)
        Dim max As New XYZ(bb.Max.X + 1, bb.Max.Y + 1, m_TopoSurface.BoundingBox(v).Max.Z)

        Dim outline As New Outline(min, max)
        Dim points As New List(Of XYZ)
        'intPoints = TryCast(m_TopoSurface.FindPoints(outline), List(Of XYZ))
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


#Region " Slab Boundary Debug "
    Const _offset As Double = 0.1

    ''' <summary>
    ''' Determine the boundary polygons of the lowest
    ''' horizontal planar face of the given solid.
    ''' </summary>
    ''' <param name="polygons">Return polygonal boundary
    ''' loops of lowest horizontal face, i.e. profile of
    ''' circumference and holes</param>
    ''' <param name="solid">Input solid</param>
    ''' <returns>False if no horizontal planar face was
    ''' found, else true</returns>
    Private Shared Function GetBoundary(polygons As List(Of List(Of XYZ)), solid As Solid) As Boolean
        Dim lowest As PlanarFace = Nothing
        Dim faces As FaceArray = solid.Faces
        For Each f As Face In faces
            Dim pf As PlanarFace = TryCast(f, PlanarFace)
            'If pf IsNot Nothing AndAlso Util.IsHorizontal(pf) Then
            If pf IsNot Nothing Then
                If (lowest Is Nothing) OrElse (pf.Origin.Z < lowest.Origin.Z) Then
                    lowest = pf
                End If
            End If
        Next
        If lowest IsNot Nothing Then
            Dim p As XYZ, q As XYZ = XYZ.Zero
            Dim first As Boolean
            Dim i As Integer, n As Integer
            Dim loops As EdgeArrayArray = lowest.EdgeLoops
            For Each [loop] As EdgeArray In loops
                Dim vertices As New List(Of XYZ)()
                first = True
                For Each e As Edge In [loop]
                    Dim points As IList(Of XYZ) = e.Tessellate()
                    p = points(0)
                    If Not first Then
                        Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" & " to equal previous end point")
                    End If
                    n = points.Count
                    q = points(n - 1)
                    For i = 0 To n - 2
                        Dim v As XYZ = points(i)
                        v -= _offset * XYZ.BasisZ
                        vertices.Add(v)
                    Next
                Next
                q -= _offset * XYZ.BasisZ
                Debug.Assert(q.IsAlmostEqualTo(vertices(0)), "expected last end point to equal" & " first start point")
                polygons.Add(vertices)
            Next
        End If
        Return lowest IsNot Nothing
    End Function

    ''' <summary>
    ''' Return all floor slab boundary loop polygons
    ''' for the given floors, offset downwards from the
    ''' bottom floor faces by a certain amount.
    ''' </summary>
    Public Shared Function GetFloorBoundaryPolygons(floors As List(Of Element), opt As Options) As List(Of List(Of XYZ))
        Dim polygons As New List(Of List(Of XYZ))()

        For Each floor As Floor In floors
            Dim geo As GeometryElement = floor.Geometry(opt)

            'GeometryObjectArray objects = geo.Objects; // 2012
            'foreach( GeometryObject obj in objects ) // 2012

            For Each obj As GeometryObject In geo
                ' 2013
                Dim solid As Solid = TryCast(obj, Solid)
                If solid IsNot Nothing Then
                    GetBoundary(polygons, solid)
                End If
            Next
        Next
        Return polygons
    End Function

#End Region


End Class
