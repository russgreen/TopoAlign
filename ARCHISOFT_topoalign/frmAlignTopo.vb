Public Class frmAlignTopo

    Dim OldVal As Boolean

    Private Sub btnAlign_Click(sender As Object, e As EventArgs) Handles btnAlign.Click
        Me.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub


    Private Sub rdoElem_CheckedChanged(sender As Object, e As EventArgs) Handles rdoElem.CheckedChanged
        If Me.rdoElem.Checked = True Then
            Me.gbFaceToUse.Enabled = True
        Else
            Me.gbFaceToUse.Enabled = False
        End If
    End Sub

    Private Sub rdoEdge_CheckedChanged(sender As Object, e As EventArgs) Handles rdoEdge.CheckedChanged

        If Me.rdoEdge.Checked = True Then
            OldVal = Me.chkRemoveInside.Checked
            Me.chkRemoveInside.Checked = False
            Me.chkRemoveInside.Enabled = False
        Else
            Me.chkRemoveInside.Enabled = True
            Me.chkRemoveInside.Checked = OldVal
        End If
    End Sub

    Private Sub frmAlignTopo_Load(sender As Object, e As EventArgs) Handles MyBase.Load

    End Sub

    Private Sub DisplayUnitcomboBox_SelectedIndexChanged(sender As Object, e As EventArgs) Handles DisplayUnitcomboBox.SelectedIndexChanged
        Me.DisplayUnitTypecomboBox.SelectedIndex = Me.DisplayUnitcomboBox.SelectedIndex
    End Sub
End Class