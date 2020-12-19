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

<Transaction(TransactionMode.Manual)>
Public Class cmdPointsAtIntersection
    Implements IExternalCommand

    Dim uiapp As UIApplication
    Dim uidoc As UIDocument
    Dim app As Autodesk.Revit.ApplicationServices.Application
    Dim doc As Document
    Dim sel As Selection

    Dim clsUtil As New Util

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

        If TypeOf doc.ActiveView Is Autodesk.Revit.DB.View3D Then
            v3d = DirectCast(doc.ActiveView, View3D)
        Else
            TaskDialog.Show("Points on surface", "You must be in a 3D view", TaskDialogCommonButtons.Ok)
            Return Result.Failed
        End If

        Dim fh As New FailureHandler()
        Dim topoFilter As New TopoPickFilter()

        Dim meshTopo As Mesh
        'Dim meshElem As Mesh

        'Plane – PlanarFace
        'Cylinder – CylindricalFace
        'Cone – ConicalFace
        'Revolved Face – RevolvedFace
        'Ruled Surface – RuledFace
        'Hermite Face – HermiteFace
        Dim pf As PlanarFace
        Dim cf As CylindricalFace
        Dim rvf As RevolvedFace
        Dim rf As RuledFace
        Dim hf As HermiteFace

        Try
            Dim refToposurface As Reference = uidoc.Selection.PickObject(ObjectType.Element, topoFilter, "Select a topographic surface")
            m_TopoSurface = TryCast(doc.GetElement(refToposurface), Autodesk.Revit.DB.Architecture.TopographySurface)

            meshTopo = TryCast(m_TopoSurface.Geometry(New Options()).First(Function(q) TypeOf q Is Mesh), Mesh)

        Catch ex As Exception
            Return Result.Failed
        End Try

        Try
            Dim refElem As Reference = uidoc.Selection.PickObject(ObjectType.Face, "Select face that intersects")
            Dim geoObj As GeometryObject = doc.GetElement(refElem).GetGeometryObjectFromReference(refElem)

            'Do we have a planarface, cylindricalface, hermiteface or ruledsurface
            pf = TryCast(geoObj, PlanarFace)
            cf = TryCast(geoObj, CylindricalFace)
            hf = TryCast(geoObj, HermiteFace)
            rvf = TryCast(geoObj, RevolvedFace)
            rf = TryCast(geoObj, RuledFace)

            'If pf IsNot Nothing Then meshElem = pf.Triangulate
            'If cf IsNot Nothing Then meshElem = cf.Triangulate
            'If hf IsNot Nothing Then meshElem = hf.Triangulate
            'If rvf IsNot Nothing Then meshElem = rvf.Triangulate
            'If rf IsNot Nothing Then meshElem = rf.Triangulate

        Catch ex As Exception
            Return Result.Failed
        End Try

        Dim m_Curves As List(Of Curve)
        m_Curves = New List(Of Curve)()

        Dim m_Points As IList(Of XYZ) = New List(Of XYZ)()

        Dim args As List(Of XYZ) = New List(Of XYZ)(3)

        Try
            For i As Integer = 0 To meshTopo.NumTriangles - 1
                Dim triangle As MeshTriangle = meshTopo.Triangle(i)

                'Dim p1 As XYZ = triangle.Vertex(0)
                'Dim p2 As XYZ = triangle.Vertex(1)
                'Dim p3 As XYZ = triangle.Vertex(2)
                'args.Clear()
                'args.Add(p1)
                'args.Add(p2)
                'args.Add(p3)

                Using t As New Transaction(doc, "Create Temp Form")
                    t.Start()

                    'Dim pl As Plane = Autodesk.Revit.DB.Plane.CreateByThreePoints(triangle.Vertex(0), triangle.Vertex(1), triangle.Vertex(2))

                    'TODO: Find a way to make a face from the mesh triangle
                    Dim m_face As PlanarFace

                    'Dim geoOpt As New Autodesk.Revit.DB.Options
                    'Dim geoElem As Autodesk.Revit.DB.GeometryElement = m_form.Geometry(geoOpt)

                    'For Each geoObj As GeometryObject In geoElem
                    '    m_face = TryCast(geoObj, PlanarFace)
                    'Next

                    Dim m_curve As Curve

                    If pf IsNot Nothing Then pf.Intersect(m_face, m_curve)
                    If cf IsNot Nothing Then cf.Intersect(m_face, m_curve)
                    If hf IsNot Nothing Then hf.Intersect(m_face, m_curve)
                    If rvf IsNot Nothing Then rvf.Intersect(m_face, m_curve)
                    If rf IsNot Nothing Then rf.Intersect(m_face, m_curve)

                    If m_curve IsNot Nothing Then
                        m_Curves.Add(m_curve)
                    End If

                    t.Dispose()
                End Using
            Next
        Catch ex As Exception
            Return Result.Failed
        End Try


    End Function

End Class
