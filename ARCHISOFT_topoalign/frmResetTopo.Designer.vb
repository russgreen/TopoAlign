<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmResetTopo
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
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.cboTopoSource = New System.Windows.Forms.ComboBox()
        Me.cboTopoTarget = New System.Windows.Forms.ComboBox()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(12, 14)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(103, 13)
        Me.Label1.TabIndex = 1
        Me.Label1.Text = "Source topo surface"
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(12, 62)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(100, 13)
        Me.Label2.TabIndex = 2
        Me.Label2.Text = "Target topo surface"
        '
        'Button1
        '
        Me.Button1.Enabled = False
        Me.Button1.Location = New System.Drawing.Point(131, 111)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(197, 55)
        Me.Button1.TabIndex = 4
        Me.Button1.Text = "Reset topo region"
        Me.Button1.UseVisualStyleBackColor = True
        '
        'cboTopoSource
        '
        Me.cboTopoSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboTopoSource.FormattingEnabled = True
        Me.cboTopoSource.Location = New System.Drawing.Point(15, 30)
        Me.cboTopoSource.Name = "cboTopoSource"
        Me.cboTopoSource.Size = New System.Drawing.Size(313, 21)
        Me.cboTopoSource.TabIndex = 5
        '
        'cboTopoTarget
        '
        Me.cboTopoTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboTopoTarget.FormattingEnabled = True
        Me.cboTopoTarget.Location = New System.Drawing.Point(13, 78)
        Me.cboTopoTarget.Name = "cboTopoTarget"
        Me.cboTopoTarget.Size = New System.Drawing.Size(315, 21)
        Me.cboTopoTarget.TabIndex = 6
        '
        'frmResetTopo
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(343, 178)
        Me.Controls.Add(Me.cboTopoTarget)
        Me.Controls.Add(Me.cboTopoSource)
        Me.Controls.Add(Me.Button1)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow
        Me.Name = "frmResetTopo"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Reset Topo Region"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Button1 As System.Windows.Forms.Button
    Friend WithEvents cboTopoSource As System.Windows.Forms.ComboBox
    Friend WithEvents cboTopoTarget As System.Windows.Forms.ComboBox
End Class
