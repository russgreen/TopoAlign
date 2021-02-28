Imports System.Windows.Forms
Imports Microsoft.AppCenter.Crashes

Public Class DialogUserConfirmation

    Private _clickResult As UserConfirmation
    Public Property ClickResult() As UserConfirmation
        Get
            Return _clickResult
        End Get
        Set(ByVal value As UserConfirmation)
            _clickResult = value
        End Set
    End Property

    Private Sub OK_Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles OK_Button.Click
        ClickResult = UserConfirmation.Send
        Me.DialogResult = DialogResult.None
        Me.Close()
    End Sub

    Private Sub Cancel_Button_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Cancel_Button.Click
        ClickResult = UserConfirmation.DontSend
        Me.DialogResult = DialogResult.None
        Me.Close()
    End Sub

End Class
