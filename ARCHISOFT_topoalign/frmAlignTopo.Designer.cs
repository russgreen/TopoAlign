using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;

namespace ARCHISOFT_topoalign
{
    [DesignerGenerated()]
    public partial class frmAlignTopo : System.Windows.Forms.Form
    {

        // Form overrides dispose to clean up the component list.
        [DebuggerNonUserCode()]
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && components is object)
                {
                    components.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [DebuggerStepThrough()]
        private void InitializeComponent()
        {
            _btnAlign = new System.Windows.Forms.Button();
            _btnAlign.Click += new EventHandler(btnAlign_Click);
            Label1 = new System.Windows.Forms.Label();
            nudDivide = new System.Windows.Forms.NumericUpDown();
            Label2 = new System.Windows.Forms.Label();
            nudVertOffset = new System.Windows.Forms.NumericUpDown();
            chkRemoveInside = new System.Windows.Forms.CheckBox();
            rdoTop = new System.Windows.Forms.RadioButton();
            rdoBottom = new System.Windows.Forms.RadioButton();
            gbFaceToUse = new System.Windows.Forms.GroupBox();
            GroupBox2 = new System.Windows.Forms.GroupBox();
            _rdoEdge = new System.Windows.Forms.RadioButton();
            _rdoEdge.CheckedChanged += new EventHandler(rdoEdge_CheckedChanged);
            _rdoElem = new System.Windows.Forms.RadioButton();
            _rdoElem.CheckedChanged += new EventHandler(rdoElem_CheckedChanged);
            lblUnits = new System.Windows.Forms.Label();
            DisplayUnitTypecomboBox = new System.Windows.Forms.ComboBox();
            _DisplayUnitcomboBox = new System.Windows.Forms.ComboBox();
            _DisplayUnitcomboBox.SelectedIndexChanged += new EventHandler(DisplayUnitcomboBox_SelectedIndexChanged);
            ((System.ComponentModel.ISupportInitialize)nudDivide).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudVertOffset).BeginInit();
            gbFaceToUse.SuspendLayout();
            GroupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // btnAlign
            // 
            _btnAlign.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            _btnAlign.Location = new System.Drawing.Point(12, 237);
            _btnAlign.Name = "_btnAlign";
            _btnAlign.Size = new System.Drawing.Size(201, 56);
            _btnAlign.TabIndex = 0;
            _btnAlign.Text = "Align Topo";
            _btnAlign.UseVisualStyleBackColor = true;
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(6, 108);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(107, 13);
            Label1.TabIndex = 1;
            Label1.Text = "Divide edge distance";
            // 
            // nudDivide
            // 
            nudDivide.DecimalPlaces = 2;
            nudDivide.Location = new System.Drawing.Point(123, 106);
            nudDivide.Maximum = new decimal(new int[] { 1000000000, 0, 0, 0 });
            nudDivide.Name = "nudDivide";
            nudDivide.Size = new System.Drawing.Size(90, 20);
            nudDivide.TabIndex = 2;
            nudDivide.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            nudDivide.Value = new decimal(new int[] { 5000, 0, 0, 0 });
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(9, 134);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(71, 13);
            Label2.TabIndex = 1;
            Label2.Text = "Vertical offset";
            // 
            // nudVertOffset
            // 
            nudVertOffset.DecimalPlaces = 2;
            nudVertOffset.Location = new System.Drawing.Point(123, 132);
            nudVertOffset.Maximum = new decimal(new int[] { 1000000000, 0, 0, 0 });
            nudVertOffset.Name = "nudVertOffset";
            nudVertOffset.Size = new System.Drawing.Size(89, 20);
            nudVertOffset.TabIndex = 2;
            nudVertOffset.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            nudVertOffset.Value = new decimal(new int[] { 50, 0, 0, 0 });
            // 
            // chkRemoveInside
            // 
            chkRemoveInside.AutoSize = true;
            chkRemoveInside.Location = new System.Drawing.Point(11, 158);
            chkRemoveInside.Name = "chkRemoveInside";
            chkRemoveInside.Size = new System.Drawing.Size(151, 17);
            chkRemoveInside.TabIndex = 3;
            chkRemoveInside.Text = "Remove inside topo points";
            chkRemoveInside.UseVisualStyleBackColor = true;
            // 
            // rdoTop
            // 
            rdoTop.AutoSize = true;
            rdoTop.Checked = true;
            rdoTop.Location = new System.Drawing.Point(14, 18);
            rdoTop.Name = "rdoTop";
            rdoTop.Size = new System.Drawing.Size(71, 17);
            rdoTop.TabIndex = 4;
            rdoTop.TabStop = true;
            rdoTop.Text = "Top Face";
            rdoTop.UseVisualStyleBackColor = true;
            // 
            // rdoBottom
            // 
            rdoBottom.AutoSize = true;
            rdoBottom.Location = new System.Drawing.Point(102, 18);
            rdoBottom.Name = "rdoBottom";
            rdoBottom.Size = new System.Drawing.Size(85, 17);
            rdoBottom.TabIndex = 4;
            rdoBottom.Text = "Bottom Face";
            rdoBottom.UseVisualStyleBackColor = true;
            // 
            // gbFaceToUse
            // 
            gbFaceToUse.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbFaceToUse.Controls.Add(rdoTop);
            gbFaceToUse.Controls.Add(rdoBottom);
            gbFaceToUse.Location = new System.Drawing.Point(12, 186);
            gbFaceToUse.Name = "gbFaceToUse";
            gbFaceToUse.Size = new System.Drawing.Size(200, 45);
            gbFaceToUse.TabIndex = 5;
            gbFaceToUse.TabStop = false;
            gbFaceToUse.Text = "Face to use";
            // 
            // GroupBox2
            // 
            GroupBox2.Controls.Add(_rdoEdge);
            GroupBox2.Controls.Add(_rdoElem);
            GroupBox2.Location = new System.Drawing.Point(12, 12);
            GroupBox2.Name = "GroupBox2";
            GroupBox2.Size = new System.Drawing.Size(200, 45);
            GroupBox2.TabIndex = 6;
            GroupBox2.TabStop = false;
            GroupBox2.Text = "Pick method";
            // 
            // rdoEdge
            // 
            _rdoEdge.AutoSize = true;
            _rdoEdge.Location = new System.Drawing.Point(126, 19);
            _rdoEdge.Name = "_rdoEdge";
            _rdoEdge.Size = new System.Drawing.Size(61, 17);
            _rdoEdge.TabIndex = 1;
            _rdoEdge.Text = "Edge(s)";
            _rdoEdge.UseVisualStyleBackColor = true;
            // 
            // rdoElem
            // 
            _rdoElem.AutoSize = true;
            _rdoElem.Checked = true;
            _rdoElem.Location = new System.Drawing.Point(6, 19);
            _rdoElem.Name = "_rdoElem";
            _rdoElem.Size = new System.Drawing.Size(95, 17);
            _rdoElem.TabIndex = 0;
            _rdoElem.TabStop = true;
            _rdoElem.Text = "Single Element";
            _rdoElem.UseVisualStyleBackColor = true;
            // 
            // lblUnits
            // 
            lblUnits.AutoSize = true;
            lblUnits.Location = new System.Drawing.Point(8, 63);
            lblUnits.Name = "lblUnits";
            lblUnits.Size = new System.Drawing.Size(57, 13);
            lblUnits.TabIndex = 1;
            lblUnits.Text = "Units used";
            // 
            // DisplayUnitTypecomboBox
            // 
            DisplayUnitTypecomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            DisplayUnitTypecomboBox.FormattingEnabled = true;
            DisplayUnitTypecomboBox.Location = new System.Drawing.Point(86, 60);
            DisplayUnitTypecomboBox.Name = "DisplayUnitTypecomboBox";
            DisplayUnitTypecomboBox.Size = new System.Drawing.Size(126, 21);
            DisplayUnitTypecomboBox.TabIndex = 7;
            DisplayUnitTypecomboBox.Visible = false;
            // 
            // DisplayUnitcomboBox
            // 
            _DisplayUnitcomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            _DisplayUnitcomboBox.FormattingEnabled = true;
            _DisplayUnitcomboBox.Location = new System.Drawing.Point(11, 79);
            _DisplayUnitcomboBox.Name = "_DisplayUnitcomboBox";
            _DisplayUnitcomboBox.Size = new System.Drawing.Size(201, 21);
            _DisplayUnitcomboBox.TabIndex = 7;
            // 
            // frmAlignTopo
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(225, 305);
            Controls.Add(_DisplayUnitcomboBox);
            Controls.Add(DisplayUnitTypecomboBox);
            Controls.Add(GroupBox2);
            Controls.Add(gbFaceToUse);
            Controls.Add(chkRemoveInside);
            Controls.Add(nudVertOffset);
            Controls.Add(nudDivide);
            Controls.Add(Label2);
            Controls.Add(lblUnits);
            Controls.Add(Label1);
            Controls.Add(_btnAlign);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Name = "frmAlignTopo";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Align Topo";
            ((System.ComponentModel.ISupportInitialize)nudDivide).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudVertOffset).EndInit();
            gbFaceToUse.ResumeLayout(false);
            gbFaceToUse.PerformLayout();
            GroupBox2.ResumeLayout(false);
            GroupBox2.PerformLayout();
            Load += new EventHandler(frmAlignTopo_Load);
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.Button _btnAlign;

        internal System.Windows.Forms.Button btnAlign
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnAlign;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnAlign != null)
                {
                    _btnAlign.Click -= btnAlign_Click;
                }

                _btnAlign = value;
                if (_btnAlign != null)
                {
                    _btnAlign.Click += btnAlign_Click;
                }
            }
        }

        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.NumericUpDown nudDivide;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.NumericUpDown nudVertOffset;
        internal System.Windows.Forms.CheckBox chkRemoveInside;
        internal System.Windows.Forms.RadioButton rdoTop;
        internal System.Windows.Forms.RadioButton rdoBottom;
        internal System.Windows.Forms.GroupBox gbFaceToUse;
        internal System.Windows.Forms.GroupBox GroupBox2;
        private System.Windows.Forms.RadioButton _rdoEdge;

        internal System.Windows.Forms.RadioButton rdoEdge
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _rdoEdge;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_rdoEdge != null)
                {
                    _rdoEdge.CheckedChanged -= rdoEdge_CheckedChanged;
                }

                _rdoEdge = value;
                if (_rdoEdge != null)
                {
                    _rdoEdge.CheckedChanged += rdoEdge_CheckedChanged;
                }
            }
        }

        private System.Windows.Forms.RadioButton _rdoElem;

        internal System.Windows.Forms.RadioButton rdoElem
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _rdoElem;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_rdoElem != null)
                {
                    _rdoElem.CheckedChanged -= rdoElem_CheckedChanged;
                }

                _rdoElem = value;
                if (_rdoElem != null)
                {
                    _rdoElem.CheckedChanged += rdoElem_CheckedChanged;
                }
            }
        }

        internal System.Windows.Forms.Label lblUnits;
        internal System.Windows.Forms.ComboBox DisplayUnitTypecomboBox;
        private System.Windows.Forms.ComboBox _DisplayUnitcomboBox;

        internal System.Windows.Forms.ComboBox DisplayUnitcomboBox
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _DisplayUnitcomboBox;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_DisplayUnitcomboBox != null)
                {
                    _DisplayUnitcomboBox.SelectedIndexChanged -= DisplayUnitcomboBox_SelectedIndexChanged;
                }

                _DisplayUnitcomboBox = value;
                if (_DisplayUnitcomboBox != null)
                {
                    _DisplayUnitcomboBox.SelectedIndexChanged += DisplayUnitcomboBox_SelectedIndexChanged;
                }
            }
        }
    }
}