Public Class frmResetTopo

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Me.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub

    Private Sub cboTopoSource_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboTopoSource.SelectedIndexChanged, cboTopoTarget.SelectedIndexChanged
        If Me.cboTopoSource.Items.Count > 0 And Me.cboTopoTarget.Items.Count > 0 Then
            Me.Button1.Enabled = True
        End If
    End Sub

End Class