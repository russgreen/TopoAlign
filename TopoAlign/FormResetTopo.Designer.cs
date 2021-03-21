
namespace TopoAlign
{
    partial class FormResetTopo
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
            this._cboTopoTarget = new System.Windows.Forms.ComboBox();
            this._cboTopoSource = new System.Windows.Forms.ComboBox();
            this._Button1 = new System.Windows.Forms.Button();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // _cboTopoTarget
            // 
            this._cboTopoTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cboTopoTarget.FormattingEnabled = true;
            this._cboTopoTarget.Location = new System.Drawing.Point(13, 73);
            this._cboTopoTarget.Name = "_cboTopoTarget";
            this._cboTopoTarget.Size = new System.Drawing.Size(315, 21);
            this._cboTopoTarget.TabIndex = 11;
            // 
            // _cboTopoSource
            // 
            this._cboTopoSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cboTopoSource.FormattingEnabled = true;
            this._cboTopoSource.Location = new System.Drawing.Point(15, 25);
            this._cboTopoSource.Name = "_cboTopoSource";
            this._cboTopoSource.Size = new System.Drawing.Size(313, 21);
            this._cboTopoSource.TabIndex = 10;
            // 
            // _Button1
            // 
            this._Button1.Enabled = false;
            this._Button1.Location = new System.Drawing.Point(131, 106);
            this._Button1.Name = "_Button1";
            this._Button1.Size = new System.Drawing.Size(197, 55);
            this._Button1.TabIndex = 9;
            this._Button1.Text = "Reset topo region";
            this._Button1.UseVisualStyleBackColor = true;
            // 
            // Label2
            // 
            this.Label2.AutoSize = true;
            this.Label2.Location = new System.Drawing.Point(12, 57);
            this.Label2.Name = "Label2";
            this.Label2.Size = new System.Drawing.Size(100, 13);
            this.Label2.TabIndex = 8;
            this.Label2.Text = "Target topo surface";
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(12, 9);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(103, 13);
            this.Label1.TabIndex = 7;
            this.Label1.Text = "Source topo surface";
            // 
            // FormResetTopo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(340, 175);
            this.Controls.Add(this._cboTopoTarget);
            this.Controls.Add(this._cboTopoSource);
            this.Controls.Add(this._Button1);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FormResetTopo";
            this.Text = "Reset Topo";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox _cboTopoTarget;
        private System.Windows.Forms.ComboBox _cboTopoSource;
        private System.Windows.Forms.Button _Button1;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label1;
    }
}