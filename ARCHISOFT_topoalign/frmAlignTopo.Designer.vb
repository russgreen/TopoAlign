<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmAlignTopo
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
        Me.btnAlign = New System.Windows.Forms.Button()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.nudDivide = New System.Windows.Forms.NumericUpDown()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.nudVertOffset = New System.Windows.Forms.NumericUpDown()
        Me.chkRemoveInside = New System.Windows.Forms.CheckBox()
        Me.rdoTop = New System.Windows.Forms.RadioButton()
        Me.rdoBottom = New System.Windows.Forms.RadioButton()
        Me.gbFaceToUse = New System.Windows.Forms.GroupBox()
        Me.GroupBox2 = New System.Windows.Forms.GroupBox()
        Me.rdoEdge = New System.Windows.Forms.RadioButton()
        Me.rdoElem = New System.Windows.Forms.RadioButton()
        Me.lblUnits = New System.Windows.Forms.Label()
        Me.DisplayUnitTypecomboBox = New System.Windows.Forms.ComboBox()
        Me.DisplayUnitcomboBox = New System.Windows.Forms.ComboBox()
        CType(Me.nudDivide, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.nudVertOffset, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.gbFaceToUse.SuspendLayout()
        Me.GroupBox2.SuspendLayout()
        Me.SuspendLayout()
        '
        'btnAlign
        '
        Me.btnAlign.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnAlign.Location = New System.Drawing.Point(12, 237)
        Me.btnAlign.Name = "btnAlign"
        Me.btnAlign.Size = New System.Drawing.Size(201, 56)
        Me.btnAlign.TabIndex = 0
        Me.btnAlign.Text = "Align Topo"
        Me.btnAlign.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(6, 108)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(107, 13)
        Me.Label1.TabIndex = 1
        Me.Label1.Text = "Divide edge distance"
        '
        'nudDivide
        '
        Me.nudDivide.DecimalPlaces = 2
        Me.nudDivide.Location = New System.Drawing.Point(123, 106)
        Me.nudDivide.Maximum = New Decimal(New Integer() {1000000000, 0, 0, 0})
        Me.nudDivide.Name = "nudDivide"
        Me.nudDivide.Size = New System.Drawing.Size(90, 20)
        Me.nudDivide.TabIndex = 2
        Me.nudDivide.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        Me.nudDivide.Value = New Decimal(New Integer() {5000, 0, 0, 0})
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(9, 134)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(71, 13)
        Me.Label2.TabIndex = 1
        Me.Label2.Text = "Vertical offset"
        '
        'nudVertOffset
        '
        Me.nudVertOffset.DecimalPlaces = 2
        Me.nudVertOffset.Location = New System.Drawing.Point(123, 132)
        Me.nudVertOffset.Maximum = New Decimal(New Integer() {1000000000, 0, 0, 0})
        Me.nudVertOffset.Name = "nudVertOffset"
        Me.nudVertOffset.Size = New System.Drawing.Size(89, 20)
        Me.nudVertOffset.TabIndex = 2
        Me.nudVertOffset.TextAlign = System.Windows.Forms.HorizontalAlignment.Right
        Me.nudVertOffset.Value = New Decimal(New Integer() {50, 0, 0, 0})
        '
        'chkRemoveInside
        '
        Me.chkRemoveInside.AutoSize = True
        Me.chkRemoveInside.Location = New System.Drawing.Point(11, 158)
        Me.chkRemoveInside.Name = "chkRemoveInside"
        Me.chkRemoveInside.Size = New System.Drawing.Size(151, 17)
        Me.chkRemoveInside.TabIndex = 3
        Me.chkRemoveInside.Text = "Remove inside topo points"
        Me.chkRemoveInside.UseVisualStyleBackColor = True
        '
        'rdoTop
        '
        Me.rdoTop.AutoSize = True
        Me.rdoTop.Checked = True
        Me.rdoTop.Location = New System.Drawing.Point(14, 18)
        Me.rdoTop.Name = "rdoTop"
        Me.rdoTop.Size = New System.Drawing.Size(71, 17)
        Me.rdoTop.TabIndex = 4
        Me.rdoTop.TabStop = True
        Me.rdoTop.Text = "Top Face"
        Me.rdoTop.UseVisualStyleBackColor = True
        '
        'rdoBottom
        '
        Me.rdoBottom.AutoSize = True
        Me.rdoBottom.Location = New System.Drawing.Point(102, 18)
        Me.rdoBottom.Name = "rdoBottom"
        Me.rdoBottom.Size = New System.Drawing.Size(85, 17)
        Me.rdoBottom.TabIndex = 4
        Me.rdoBottom.Text = "Bottom Face"
        Me.rdoBottom.UseVisualStyleBackColor = True
        '
        'gbFaceToUse
        '
        Me.gbFaceToUse.Anchor = CType(((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.gbFaceToUse.Controls.Add(Me.rdoTop)
        Me.gbFaceToUse.Controls.Add(Me.rdoBottom)
        Me.gbFaceToUse.Location = New System.Drawing.Point(12, 186)
        Me.gbFaceToUse.Name = "gbFaceToUse"
        Me.gbFaceToUse.Size = New System.Drawing.Size(200, 45)
        Me.gbFaceToUse.TabIndex = 5
        Me.gbFaceToUse.TabStop = False
        Me.gbFaceToUse.Text = "Face to use"
        '
        'GroupBox2
        '
        Me.GroupBox2.Controls.Add(Me.rdoEdge)
        Me.GroupBox2.Controls.Add(Me.rdoElem)
        Me.GroupBox2.Location = New System.Drawing.Point(12, 12)
        Me.GroupBox2.Name = "GroupBox2"
        Me.GroupBox2.Size = New System.Drawing.Size(200, 45)
        Me.GroupBox2.TabIndex = 6
        Me.GroupBox2.TabStop = False
        Me.GroupBox2.Text = "Pick method"
        '
        'rdoEdge
        '
        Me.rdoEdge.AutoSize = True
        Me.rdoEdge.Location = New System.Drawing.Point(126, 19)
        Me.rdoEdge.Name = "rdoEdge"
        Me.rdoEdge.Size = New System.Drawing.Size(61, 17)
        Me.rdoEdge.TabIndex = 1
        Me.rdoEdge.Text = "Edge(s)"
        Me.rdoEdge.UseVisualStyleBackColor = True
        '
        'rdoElem
        '
        Me.rdoElem.AutoSize = True
        Me.rdoElem.Checked = True
        Me.rdoElem.Location = New System.Drawing.Point(6, 19)
        Me.rdoElem.Name = "rdoElem"
        Me.rdoElem.Size = New System.Drawing.Size(95, 17)
        Me.rdoElem.TabIndex = 0
        Me.rdoElem.TabStop = True
        Me.rdoElem.Text = "Single Element"
        Me.rdoElem.UseVisualStyleBackColor = True
        '
        'lblUnits
        '
        Me.lblUnits.AutoSize = True
        Me.lblUnits.Location = New System.Drawing.Point(8, 63)
        Me.lblUnits.Name = "lblUnits"
        Me.lblUnits.Size = New System.Drawing.Size(57, 13)
        Me.lblUnits.TabIndex = 1
        Me.lblUnits.Text = "Units used"
        '
        'DisplayUnitTypecomboBox
        '
        Me.DisplayUnitTypecomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.DisplayUnitTypecomboBox.FormattingEnabled = True
        Me.DisplayUnitTypecomboBox.Location = New System.Drawing.Point(86, 60)
        Me.DisplayUnitTypecomboBox.Name = "DisplayUnitTypecomboBox"
        Me.DisplayUnitTypecomboBox.Size = New System.Drawing.Size(126, 21)
        Me.DisplayUnitTypecomboBox.TabIndex = 7
        Me.DisplayUnitTypecomboBox.Visible = False
        '
        'DisplayUnitcomboBox
        '
        Me.DisplayUnitcomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.DisplayUnitcomboBox.FormattingEnabled = True
        Me.DisplayUnitcomboBox.Location = New System.Drawing.Point(11, 79)
        Me.DisplayUnitcomboBox.Name = "DisplayUnitcomboBox"
        Me.DisplayUnitcomboBox.Size = New System.Drawing.Size(201, 21)
        Me.DisplayUnitcomboBox.TabIndex = 7
        '
        'frmAlignTopo
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(225, 305)
        Me.Controls.Add(Me.DisplayUnitcomboBox)
        Me.Controls.Add(Me.DisplayUnitTypecomboBox)
        Me.Controls.Add(Me.GroupBox2)
        Me.Controls.Add(Me.gbFaceToUse)
        Me.Controls.Add(Me.chkRemoveInside)
        Me.Controls.Add(Me.nudVertOffset)
        Me.Controls.Add(Me.nudDivide)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.lblUnits)
        Me.Controls.Add(Me.Label1)
        Me.Controls.Add(Me.btnAlign)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow
        Me.Name = "frmAlignTopo"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Align Topo"
        CType(Me.nudDivide, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.nudVertOffset, System.ComponentModel.ISupportInitialize).EndInit()
        Me.gbFaceToUse.ResumeLayout(False)
        Me.gbFaceToUse.PerformLayout()
        Me.GroupBox2.ResumeLayout(False)
        Me.GroupBox2.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub
    Friend WithEvents btnAlign As System.Windows.Forms.Button
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents nudDivide As System.Windows.Forms.NumericUpDown
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents nudVertOffset As System.Windows.Forms.NumericUpDown
    Friend WithEvents chkRemoveInside As System.Windows.Forms.CheckBox
    Friend WithEvents rdoTop As System.Windows.Forms.RadioButton
    Friend WithEvents rdoBottom As System.Windows.Forms.RadioButton
    Friend WithEvents gbFaceToUse As System.Windows.Forms.GroupBox
    Friend WithEvents GroupBox2 As System.Windows.Forms.GroupBox
    Friend WithEvents rdoEdge As System.Windows.Forms.RadioButton
    Friend WithEvents rdoElem As System.Windows.Forms.RadioButton
    Friend WithEvents lblUnits As System.Windows.Forms.Label
    Friend WithEvents DisplayUnitTypecomboBox As System.Windows.Forms.ComboBox
    Friend WithEvents DisplayUnitcomboBox As System.Windows.Forms.ComboBox
End Class
