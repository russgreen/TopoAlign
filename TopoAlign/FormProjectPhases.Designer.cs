
namespace TopoAlign
{
    partial class FormProjectPhases
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
            this.cboPhaseTarget = new System.Windows.Forms.ComboBox();
            this.cboPhaseSource = new System.Windows.Forms.ComboBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cboPhaseTarget
            // 
            this.cboPhaseTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboPhaseTarget.FormattingEnabled = true;
            this.cboPhaseTarget.Location = new System.Drawing.Point(13, 73);
            this.cboPhaseTarget.Name = "cboPhaseTarget";
            this.cboPhaseTarget.Size = new System.Drawing.Size(315, 21);
            this.cboPhaseTarget.TabIndex = 16;
            // 
            // cboPhaseSource
            // 
            this.cboPhaseSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboPhaseSource.FormattingEnabled = true;
            this.cboPhaseSource.Location = new System.Drawing.Point(15, 25);
            this.cboPhaseSource.Name = "cboPhaseSource";
            this.cboPhaseSource.Size = new System.Drawing.Size(313, 21);
            this.cboPhaseSource.TabIndex = 15;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Enabled = false;
            this.btnOK.Location = new System.Drawing.Point(212, 112);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(116, 44);
            this.btnOK.TabIndex = 14;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // Label2
            // 
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(12, 57);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(141, 13);
            this.Label2.TabIndex = 13;
            this.Label2.Text = "Phase of target topo surface";
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 9);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(146, 13);
            this.Label1.TabIndex = 12;
            this.Label1.Text = "Phase of source topo surface";
            // 
            // FormProjectPhases
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(342, 174);
            this.Controls.Add(this.cboPhaseTarget);
            this.Controls.Add(this.cboPhaseSource);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FormProjectPhases";
            this.Text = "Project Phases";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.ComboBox cboPhaseTarget;
        internal System.Windows.Forms.ComboBox cboPhaseSource;
        internal System.Windows.Forms.Button btnOK;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label1;
    }
}