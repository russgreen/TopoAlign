Partial Public Class ProgressForm
    Inherits System.Windows.Forms.Form
    Private _format As String

    Private abortFlag As Boolean = False

    ''' <summary>
    ''' Set up progress bar form and immediately display it modelessly.
    ''' </summary>
    ''' <param name="caption">Form caption</param>
    ''' <param name="format">Progress message string</param>
    ''' <param name="max">Number of elements to process</param>
    Public Sub New(caption As String, format As String, max As Integer)
        _format = format
        InitializeComponent()
        Text = caption
        Label1.Text = If((format Is Nothing), caption, String.Format(format, 0))
        ProgressBar1.Minimum = 0
        ProgressBar1.Maximum = max
        ProgressBar1.Value = 0
        Show()
        System.Windows.Forms.Application.DoEvents()
    End Sub

    Public Sub Increment()
        ProgressBar1.Value += 1
        If _format IsNot Nothing Then
            Label1.Text = String.Format(_format, ProgressBar1.Value)
        End If
        System.Windows.Forms.Application.DoEvents()
    End Sub

    Public Function getAbortFlag() As Boolean
        Return abortFlag
    End Function


    Private Sub btnAbort_Click(sender As Object, e As EventArgs) Handles btnAbort.Click
        btnAbort.Text = "Aborting..."
        abortFlag = True
    End Sub
End Class