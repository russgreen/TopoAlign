<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmProjectPhases
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.cboPhaseTarget = New System.Windows.Forms.ComboBox()
        Me.cboPhaseSource = New System.Windows.Forms.ComboBox()
        Me.btnOK = New System.Windows.Forms.Button()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.SuspendLayout()
        '
        'cboPhaseTarget
        '
        Me.cboPhaseTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboPhaseTarget.FormattingEnabled = True
        Me.cboPhaseTarget.Location = New System.Drawing.Point(13, 73)
        Me.cboPhaseTarget.Name = "cboPhaseTarget"
        Me.cboPhaseTarget.Size = New System.Drawing.Size(315, 21)
        Me.cboPhaseTarget.TabIndex = 11
        '
        'cboPhaseSource
        '
        Me.cboPhaseSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboPhaseSource.FormattingEnabled = True
        Me.cboPhaseSource.Location = New System.Drawing.Point(15, 25)
        Me.cboPhaseSource.Name = "cboPhaseSource"
        Me.cboPhaseSource.Size = New System.Drawing.Size(313, 21)
        Me.cboPhaseSource.TabIndex = 10
        '
        'btnOK
        '
        Me.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.btnOK.Enabled = False
        Me.btnOK.Location = New System.Drawing.Point(212, 112)
        Me.btnOK.Name = "btnOK"
        Me.btnOK.Size = New System.Drawing.Size(116, 44)
        Me.btnOK.TabIndex = 9
        Me.btnOK.Text = "OK"
        Me.btnOK.UseVisualStyleBackColor = True
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(12, 57)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(141, 13)
        Me.Label2.TabIndex = 8
        Me.Label2.Text = "Phase of target topo surface"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 9)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(146, 13)
        Me.Label1.TabIndex = 7
        Me.Label1.Text = "Phase of source topo surface"
        '
        'frmProjectPhases
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(340, 171)
        Me.Controls.Add(Me.cboPhaseTarget)
        Me.Controls.Add(Me.cboPhaseSource)
        Me.Controls.Add(Me.btnOK)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow
        Me.Name = "frmProjectPhases"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Project Phases"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents cboPhaseTarget As System.Windows.Forms.ComboBox
    Friend WithEvents cboPhaseSource As System.Windows.Forms.ComboBox
    Friend WithEvents btnOK As System.Windows.Forms.Button
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Label1 As System.Windows.Forms.Label
End Class
