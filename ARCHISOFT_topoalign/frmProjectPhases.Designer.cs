using System.Diagnostics;

namespace ARCHISOFT_topoalign
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class frmProjectPhases : System.Windows.Forms.Form
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
            cboPhaseTarget = new System.Windows.Forms.ComboBox();
            cboPhaseSource = new System.Windows.Forms.ComboBox();
            btnOK = new System.Windows.Forms.Button();
            Label2 = new System.Windows.Forms.Label();
            Label1 = new System.Windows.Forms.Label();
            SuspendLayout();
            // 
            // cboPhaseTarget
            // 
            cboPhaseTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPhaseTarget.FormattingEnabled = true;
            cboPhaseTarget.Location = new System.Drawing.Point(13, 73);
            cboPhaseTarget.Name = "cboPhaseTarget";
            cboPhaseTarget.Size = new System.Drawing.Size(315, 21);
            cboPhaseTarget.TabIndex = 11;
            // 
            // cboPhaseSource
            // 
            cboPhaseSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cboPhaseSource.FormattingEnabled = true;
            cboPhaseSource.Location = new System.Drawing.Point(15, 25);
            cboPhaseSource.Name = "cboPhaseSource";
            cboPhaseSource.Size = new System.Drawing.Size(313, 21);
            cboPhaseSource.TabIndex = 10;
            // 
            // btnOK
            // 
            btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            btnOK.Enabled = false;
            btnOK.Location = new System.Drawing.Point(212, 112);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(116, 44);
            btnOK.TabIndex = 9;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 57);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(141, 13);
            Label2.TabIndex = 8;
            Label2.Text = "Phase of target topo surface";
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 9);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(146, 13);
            Label1.TabIndex = 7;
            Label1.Text = "Phase of source topo surface";
            // 
            // frmProjectPhases
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(340, 171);
            Controls.Add(cboPhaseTarget);
            Controls.Add(cboPhaseSource);
            Controls.Add(btnOK);
            Controls.Add(Label2);
            Controls.Add(Label1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Name = "frmProjectPhases";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Project Phases";
            ResumeLayout(false);
            PerformLayout();
        }

        internal System.Windows.Forms.ComboBox cboPhaseTarget;
        internal System.Windows.Forms.ComboBox cboPhaseSource;
        internal System.Windows.Forms.Button btnOK;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label1;
    }
}