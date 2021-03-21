
namespace TopoAlign
{
    partial class FormAlignTopo
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.GroupBox2 = new System.Windows.Forms.GroupBox();
            this.rdoEdge = new System.Windows.Forms.RadioButton();
            this.rdoElem = new System.Windows.Forms.RadioButton();
            this.DisplayUnitcomboBox = new System.Windows.Forms.ComboBox();
            this.gbFaceToUse = new System.Windows.Forms.GroupBox();
            this.rdoTop = new System.Windows.Forms.RadioButton();
            this.rdoBottom = new System.Windows.Forms.RadioButton();
            this.chkRemoveInside = new System.Windows.Forms.CheckBox();
            this.nudVertOffset = new System.Windows.Forms.NumericUpDown();
            this.nudDivide = new System.Windows.Forms.NumericUpDown();
            this.Label2 = new System.Windows.Forms.Label();
            this.lblUnits = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.btnAlign = new System.Windows.Forms.Button();
            this.DisplayUnitTypecomboBox = new System.Windows.Forms.ComboBox();
            this.GroupBox2.SuspendLayout();
            this.gbFaceToUse.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudVertOffset)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDivide)).BeginInit();
            this.SuspendLayout();
            // 
            // GroupBox2
            // 
            this.GroupBox2.Controls.Add(this.rdoEdge);
            this.GroupBox2.Controls.Add(this.rdoElem);
            this.GroupBox2.Location = new System.Drawing.Point(12, 12);
            this.GroupBox2.Name = "GroupBox2";
            this.GroupBox2.Size = new System.Drawing.Size(200, 45);
            this.GroupBox2.TabIndex = 7;
            this.GroupBox2.TabStop = false;
            this.GroupBox2.Text = "Pick method";
            // 
            // rdoEdge
            // 
            this.rdoEdge.AutoSize = true;
            this.rdoEdge.Location = new System.Drawing.Point(126, 19);
            this.rdoEdge.Name = "rdoEdge";
            this.rdoEdge.Size = new System.Drawing.Size(61, 17);
            this.rdoEdge.TabIndex = 1;
            this.rdoEdge.Text = "Edge(s)";
            this.rdoEdge.UseVisualStyleBackColor = true;
            this.rdoEdge.CheckedChanged += new System.EventHandler(this.rdoEdge_CheckedChanged);
            // 
            // rdoElem
            // 
            this.rdoElem.AutoSize = true;
            this.rdoElem.Checked = true;
            this.rdoElem.Location = new System.Drawing.Point(6, 19);
            this.rdoElem.Name = "rdoElem";
            this.rdoElem.Size = new System.Drawing.Size(95, 17);
            this.rdoElem.TabIndex = 0;
            this.rdoElem.TabStop = true;
            this.rdoElem.Text = "Single Element";
            this.rdoElem.UseVisualStyleBackColor = true;
            this.rdoElem.CheckedChanged += new System.EventHandler(this.rdoElem_CheckedChanged);
            // 
            // DisplayUnitcomboBox
            // 
            this.DisplayUnitcomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DisplayUnitcomboBox.FormattingEnabled = true;
            this.DisplayUnitcomboBox.Location = new System.Drawing.Point(12, 76);
            this.DisplayUnitcomboBox.Name = "DisplayUnitcomboBox";
            this.DisplayUnitcomboBox.Size = new System.Drawing.Size(201, 21);
            this.DisplayUnitcomboBox.TabIndex = 16;
            this.DisplayUnitcomboBox.SelectedIndexChanged += new System.EventHandler(this.DisplayUnitcomboBox_SelectedIndexChanged);
            // 
            // gbFaceToUse
            // 
            this.gbFaceToUse.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbFaceToUse.Controls.Add(this.rdoTop);
            this.gbFaceToUse.Controls.Add(this.rdoBottom);
            this.gbFaceToUse.Location = new System.Drawing.Point(13, 183);
            this.gbFaceToUse.Name = "gbFaceToUse";
            this.gbFaceToUse.Size = new System.Drawing.Size(200, 45);
            this.gbFaceToUse.TabIndex = 15;
            this.gbFaceToUse.TabStop = false;
            this.gbFaceToUse.Text = "Face to use";
            // 
            // rdoTop
            // 
            this.rdoTop.AutoSize = true;
            this.rdoTop.Checked = true;
            this.rdoTop.Location = new System.Drawing.Point(14, 18);
            this.rdoTop.Name = "rdoTop";
            this.rdoTop.Size = new System.Drawing.Size(71, 17);
            this.rdoTop.TabIndex = 4;
            this.rdoTop.TabStop = true;
            this.rdoTop.Text = "Top Face";
            this.rdoTop.UseVisualStyleBackColor = true;
            // 
            // rdoBottom
            // 
            this.rdoBottom.AutoSize = true;
            this.rdoBottom.Location = new System.Drawing.Point(102, 18);
            this.rdoBottom.Name = "rdoBottom";
            this.rdoBottom.Size = new System.Drawing.Size(85, 17);
            this.rdoBottom.TabIndex = 4;
            this.rdoBottom.Text = "Bottom Face";
            this.rdoBottom.UseVisualStyleBackColor = true;
            // 
            // chkRemoveInside
            // 
            this.chkRemoveInside.AutoSize = true;
            this.chkRemoveInside.Location = new System.Drawing.Point(12, 155);
            this.chkRemoveInside.Name = "chkRemoveInside";
            this.chkRemoveInside.Size = new System.Drawing.Size(151, 17);
            this.chkRemoveInside.TabIndex = 14;
            this.chkRemoveInside.Text = "Remove inside topo points";
            this.chkRemoveInside.UseVisualStyleBackColor = true;
            // 
            // nudVertOffset
            // 
            this.nudVertOffset.DecimalPlaces = 2;
            this.nudVertOffset.Location = new System.Drawing.Point(124, 129);
            this.nudVertOffset.Maximum = new decimal(new int[] {
            1000000000,
            0,
            0,
            0});
            this.nudVertOffset.Name = "nudVertOffset";
            this.nudVertOffset.Size = new System.Drawing.Size(89, 20);
            this.nudVertOffset.TabIndex = 12;
            this.nudVertOffset.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudVertOffset.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
            // 
            // nudDivide
            // 
            this.nudDivide.DecimalPlaces = 2;
            this.nudDivide.Location = new System.Drawing.Point(124, 103);
            this.nudDivide.Maximum = new decimal(new int[] {
            1000000000,
            0,
            0,
            0});
            this.nudDivide.Name = "nudDivide";
            this.nudDivide.Size = new System.Drawing.Size(90, 20);
            this.nudDivide.TabIndex = 13;
            this.nudDivide.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudDivide.Value = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            // 
            // Label2
            // 
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(10, 131);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(71, 13);
            this.Label2.TabIndex = 9;
            this.Label2.Text = "Vertical offset";
            // 
            // lblUnits
            // 
            this.lblUnits.AutoSize = true;
            this.lblUnits.Location = new System.Drawing.Point(9, 60);
            this.lblUnits.Name = "lblUnits";
            this.lblUnits.Size = new System.Drawing.Size(57, 13);
            this.lblUnits.TabIndex = 10;
            this.lblUnits.Text = "Units used";
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(7, 105);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(107, 13);
            this.Label1.TabIndex = 11;
            this.Label1.Text = "Divide edge distance";
            // 
            // btnAlign
            // 
            this.btnAlign.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAlign.Location = new System.Drawing.Point(13, 234);
            this.btnAlign.Name = "btnAlign";
            this.btnAlign.Size = new System.Drawing.Size(201, 56);
            this.btnAlign.TabIndex = 8;
            this.btnAlign.Text = "Align Topo";
            this.btnAlign.UseVisualStyleBackColor = true;
            this.btnAlign.Click += new System.EventHandler(this.btnAlign_Click);
            // 
            // DisplayUnitTypecomboBox
            // 
            this.DisplayUnitTypecomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DisplayUnitTypecomboBox.FormattingEnabled = true;
            this.DisplayUnitTypecomboBox.Location = new System.Drawing.Point(88, 57);
            this.DisplayUnitTypecomboBox.Name = "DisplayUnitTypecomboBox";
            this.DisplayUnitTypecomboBox.Size = new System.Drawing.Size(126, 21);
            this.DisplayUnitTypecomboBox.TabIndex = 17;
            this.DisplayUnitTypecomboBox.Visible = false;
            // 
            // FormAlignTopo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(225, 305);
            this.Controls.Add(this.DisplayUnitTypecomboBox);
            this.Controls.Add(this.DisplayUnitcomboBox);
            this.Controls.Add(this.gbFaceToUse);
            this.Controls.Add(this.chkRemoveInside);
            this.Controls.Add(this.nudVertOffset);
            this.Controls.Add(this.nudDivide);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.lblUnits);
            this.Controls.Add(this.Label1);
            this.Controls.Add(this.btnAlign);
            this.Controls.Add(this.GroupBox2);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FormAlignTopo";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Align Topo";
            this.GroupBox2.ResumeLayout(false);
            this.GroupBox2.PerformLayout();
            this.gbFaceToUse.ResumeLayout(false);
            this.gbFaceToUse.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudVertOffset)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDivide)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.GroupBox GroupBox2;
        internal System.Windows.Forms.GroupBox gbFaceToUse;
        internal System.Windows.Forms.RadioButton rdoTop;
        internal System.Windows.Forms.RadioButton rdoBottom;
        internal System.Windows.Forms.CheckBox chkRemoveInside;
        internal System.Windows.Forms.NumericUpDown nudVertOffset;
        internal System.Windows.Forms.NumericUpDown nudDivide;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label lblUnits;
        internal System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Button btnAlign;
        internal System.Windows.Forms.RadioButton rdoEdge;
        internal System.Windows.Forms.RadioButton rdoElem;
        internal System.Windows.Forms.ComboBox DisplayUnitcomboBox;
        internal System.Windows.Forms.ComboBox DisplayUnitTypecomboBox;
    }
}