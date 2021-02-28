#Region "Imported Namespaces"
Imports System
Imports System.Collections.Generic
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.Attributes
Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports Autodesk.Revit.UI.Selection
Imports System.Windows.Forms
Imports Autodesk.Revit.DB.Architecture
Imports Microsoft.AppCenter.Crashes

#End Region


<Transaction(TransactionMode.Manual)>
Public Class cmdResetTopoRegion
    Implements IExternalCommand

    Dim uiapp As UIApplication
    Dim uidoc As UIDocument
    Dim app As Autodesk.Revit.ApplicationServices.Application
    Dim doc As Document

    Dim clsUtil As New Util

    Dim m_TopoSurface1 As Autodesk.Revit.DB.Architecture.TopographySurface
    Dim m_TopoSurface2 As Autodesk.Revit.DB.Architecture.TopographySurface

    Dim sourcePhaseName As String = "Existing"
    Dim targetPhaseName As String = "New Construction"


    Public Function Execute(commandData As ExternalCommandData, ByRef message As String, elements As ElementSet) As Result Implements IExternalCommand.Execute
        'Crashes.GenerateTestCrash()

        uiapp = commandData.Application
        uidoc = uiapp.ActiveUIDocument
        app = uiapp.Application
        doc = uidoc.Document

        'check if the active view is a plan view
        If doc.ActiveView.ViewType <> ViewType.FloorPlan Then
            TaskDialog.Show("Topo Reset", "Please make sure you are in a plan view before running this command", TaskDialogCommonButtons.Ok)
            Return Result.Failed
        End If

        Dim frm As New frmProjectPhases
        With frm
            'TODO: load phases to combo
            Dim comboPhases As New Dictionary(Of Integer, String)
            For Each m_phase As Phase In doc.Phases
                comboPhases.Add(m_phase.Id.IntegerValue, m_phase.Name)
            Next

            If comboPhases.Count > 0 Then
                .cboPhaseSource.DisplayMember = "Value"
                .cboPhaseSource.ValueMember = "Value" '"Key"
                .cboPhaseSource.DataSource = New BindingSource(comboPhases, Nothing)
                .cboPhaseSource.SelectedIndex = 0

                .cboPhaseTarget.DisplayMember = "Value"
                .cboPhaseTarget.ValueMember = "Value" '"Key"
                .cboPhaseTarget.DataSource = New BindingSource(comboPhases, Nothing)
                .cboPhaseTarget.SelectedIndex = 1

                .btnOK.Enabled = True
            End If

            If .ShowDialog = DialogResult.OK Then
                sourcePhaseName = .cboPhaseSource.SelectedValue.ToString
                targetPhaseName = .cboPhaseTarget.SelectedValue.ToString

                Dim sourcePhase As Phase = New FilteredElementCollector(doc).OfClass(GetType(Phase)).Cast(Of Phase)().FirstOrDefault(Function(q) q.Name = sourcePhaseName)
                Dim targetPhase As Phase = New FilteredElementCollector(doc).OfClass(GetType(Phase)).Cast(Of Phase)().FirstOrDefault(Function(q) q.Name = targetPhaseName)
                Dim topoList As IList(Of Element) = New FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Topography).ToList()
                Dim fh As New FailureHandler()

                Dim frm1 As New frmResetTopo
                With frm1
                    Try
                        'load existing topo(s) to combo
                        Dim comboSource As New Dictionary(Of Integer, String)
                        Dim comboTarget As New Dictionary(Of Integer, String)

                        For Each m_Topo As TopographySurface In topoList

                            If m_Topo.IsSiteSubRegion = False Then
                                Try
                                    Dim phaseParam As Autodesk.Revit.DB.Parameter = m_Topo.GetParameters("Phase Created").Item(0) ' clsUtil.FindParameterByName(m_Topo, "Phase Created")
                                    Dim nameParam As Autodesk.Revit.DB.Parameter = m_Topo.GetParameters("Name").Item(0) 'clsUtil.FindParameterByName(m_Topo, "Name")

                                    If phaseParam.AsValueString = sourcePhaseName Then
                                        comboSource.Add(m_Topo.Id.IntegerValue, "Toposurface (" & nameParam.AsString & ") - " & phaseParam.AsValueString)
                                    End If

                                    If phaseParam.AsValueString = targetPhaseName Then
                                        comboTarget.Add(m_Topo.Id.IntegerValue, "Toposurface (" & nameParam.AsString & ") - " & phaseParam.AsValueString)
                                    End If

                                Catch ex As Exception

                                End Try
                            End If


                        Next

                        'load topos to source combo
                        If comboSource.Count > 0 Then
                            .cboTopoSource.DisplayMember = "Value"
                            .cboTopoSource.ValueMember = "Key"
                            .cboTopoSource.DataSource = New BindingSource(comboSource, Nothing)
                            '.cboTopoSource.Items.AddRange(comboSource.Keys.ToArray)
                            .cboTopoSource.SelectedIndex = 0
                        End If

                        'load topos to target combo
                        If comboTarget.Count > 0 Then
                            .cboTopoTarget.DisplayMember = "Value"
                            .cboTopoTarget.ValueMember = "Key"
                            .cboTopoTarget.DataSource = New BindingSource(comboTarget, Nothing)
                            '.cboTopoTarget.Items.AddRange(comboTarget.Keys.ToArray)
                            .cboTopoTarget.SelectedIndex = 0
                        End If


                        If .ShowDialog = DialogResult.OK Then
                            'pick region
                            Dim pb As PickedBox = uidoc.Selection.PickBox(PickBoxStyle.Enclosing, "Pick a rectatangle (lower left to upper right) within which the target topo points will be reset from the source topo")

                            'Dim p1 As XYZ = uidoc.Selection.PickPoint("Pick lower left corner of rectangle area to reset")
                            'Dim p2 As XYZ = uidoc.Selection.PickPoint("Pick upper right corner of rectangle area to reset")
                            Dim llX As Double
                            Dim llY As Double
                            Dim urX As Double
                            Dim urY As Double
                            If pb.Min.X < pb.Max.X Then
                                llX = pb.Min.X
                                urX = pb.Max.X
                            Else
                                llX = pb.Max.X
                                urX = pb.Min.X
                            End If
                            If pb.Min.Y < pb.Max.Y Then
                                llY = pb.Min.Y
                                urY = pb.Max.Y
                            Else
                                llY = pb.Max.Y
                                urY = pb.Min.Y
                            End If

                            Dim p1 As New XYZ(llX, llY, pb.Min.Z) ' = pb.Min
                            Dim p2 As New XYZ(urX, urY, pb.Max.Z) ' = pb.Max

                            'If p2.X < p1.X Or p2.Y < p1.Y Then
                            '    p2 = uidoc.Selection.PickPoint("Pick UPPER RIGHT corner of rectangle area to reset")
                            'End If

                            'established the bounding box of the source and target topos to get min max Z values
                            Dim topoSource As TopographySurface = Nothing
                            Dim topoTarget As TopographySurface = Nothing

                            For Each m_Topo As Element In topoList
                                If m_Topo.Id.ToString = .cboTopoSource.SelectedValue.ToString Then
                                    topoSource = DirectCast(m_Topo, TopographySurface)
                                ElseIf m_Topo.Id.ToString = .cboTopoTarget.SelectedValue.ToString Then
                                    topoTarget = DirectCast(m_Topo, TopographySurface)
                                End If
                            Next

                            If topoSource Is Nothing Or topoTarget Is Nothing Then
                                Return Result.Failed
                                Exit Function
                            End If

                            Dim v As Autodesk.Revit.DB.View = doc.ActiveView ' Nothing
                            Dim ptsExisting As New List(Of XYZ)
                            Dim bb As BoundingBoxXYZ = topoSource.BoundingBox(v)

                            Using tes As New Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Reset topo")
                                tes.Start(topoSource.Id)
                                Dim opt As New Options()
                                opt.ComputeReferences = True

                                'get points in region from topoSource
                                Dim min As New XYZ(p1.X, p1.Y, topoSource.BoundingBox(v).Min.Z - 10)
                                Dim max As New XYZ(p2.X, p2.Y, topoSource.BoundingBox(v).Max.Z + 10)

                                bb.Min = min
                                bb.Max = max

                                Dim m_Outline As New Outline(min, max)
                                'ptsExisting = TryCast(topoSource.FindPoints(m_Outline), List(Of XYZ))

                                Dim pts As New List(Of XYZ)
                                pts = TryCast(topoSource.GetInteriorPoints, List(Of XYZ))

                                For Each pt As XYZ In pts
                                    If m_Outline.Contains(pt, 0.000000001) Then
                                        ptsExisting.Add(pt)
                                    End If
                                    'If clsUtil.BoundingBoxXyzContains(bb, pt) Then
                                    '    ptsExisting.Add(p)
                                    'End If
                                Next

                                tes.Commit(fh)
                            End Using

                            Using tes As New Autodesk.Revit.DB.Architecture.TopographyEditScope(doc, "Reset topo")
                                tes.Start(topoTarget.Id)
                                Dim opt As New Options()
                                opt.ComputeReferences = True

                                Dim min As New XYZ(p1.X, p1.Y, topoTarget.BoundingBox(v).Min.Z - 10)
                                Dim max As New XYZ(p2.X, p2.Y, topoTarget.BoundingBox(v).Max.Z + 10)

                                bb.Min = min
                                bb.Max = max

                                Dim ptsToDelete As New List(Of XYZ)
                                Dim m_Outline As New Outline(min, max)
                                'ptsToDelete = TryCast(topoTarget.FindPoints(m_Outline), List(Of XYZ))

                                Dim pts As New List(Of XYZ)
                                pts = TryCast(topoTarget.GetInteriorPoints, List(Of XYZ))
                                For Each pt As XYZ In pts
                                    If m_Outline.Contains(pt, 0.000000001) Then
                                        ptsToDelete.Add(pt)
                                    End If
                                    'If clsUtil.BoundingBoxXyzContains(bb, pt) = True Then
                                    '    ptsToDelete.Add(p)
                                    'End If
                                Next

                                If ptsToDelete.Count > 0 Then
                                    Using t As New Transaction(doc, "removing points")
                                        t.Start()
                                        topoTarget.DeletePoints(ptsToDelete)
                                        t.Commit()
                                    End Using
                                End If


                                'add points to topoTarget
                                If ptsExisting.Count > 0 Then
                                    Dim comparer As New XyzEqualityComparer '(0.01)
                                    Using t As New Transaction(doc, "add points")
                                        t.Start()
                                        topoTarget.AddPoints(ptsExisting.Distinct(comparer).ToList)
                                        t.Commit()
                                    End Using
                                End If

                                tes.Commit(fh)
                            End Using

                        End If
                    Catch generatedExceptionName As Autodesk.Revit.Exceptions.OperationCanceledException

                    Catch ex As Exception
                        Return Result.Failed
                    End Try
                End With
            End If
        End With




        Return Result.Succeeded
    End Function

    Private Function PointString(p As XYZ) As String
        Return p.X.ToString & ", " & p.Y.ToString & ", " & p.Z.ToString


        Throw New NotImplementedException
    End Function


End Class
