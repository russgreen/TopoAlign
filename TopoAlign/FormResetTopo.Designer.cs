
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
            this.cboTopoTarget = new System.Windows.Forms.ComboBox();
            this.cboTopoSource = new System.Windows.Forms.ComboBox();
            this.Button_OK = new System.Windows.Forms.Button();
            this.Label2 = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cboTopoTarget
            // 
            this.cboTopoTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTopoTarget.FormattingEnabled = true;
            this.cboTopoTarget.Location = new System.Drawing.Point(13, 73);
            this.cboTopoTarget.Name = "cboTopoTarget";
            this.cboTopoTarget.Size = new System.Drawing.Size(315, 21);
            this.cboTopoTarget.TabIndex = 11;
            // 
            // cboTopoSource
            // 
            this.cboTopoSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboTopoSource.FormattingEnabled = true;
            this.cboTopoSource.Location = new System.Drawing.Point(15, 25);
            this.cboTopoSource.Name = "cboTopoSource";
            this.cboTopoSource.Size = new System.Drawing.Size(313, 21);
            this.cboTopoSource.TabIndex = 10;
            this.cboTopoSource.SelectedIndexChanged += new System.EventHandler(this.cboTopoSource_SelectedIndexChanged);
            // 
            // Button_OK
            // 
            this.Button_OK.Enabled = false;
            this.Button_OK.Location = new System.Drawing.Point(131, 106);
            this.Button_OK.Name = "Button_OK";
            this.Button_OK.Size = new System.Drawing.Size(197, 55);
            this.Button_OK.TabIndex = 9;
            this.Button_OK.Text = "Reset topo region";
            this.Button_OK.UseVisualStyleBackColor = true;
            this.Button_OK.Click += new System.EventHandler(this.Button_OK_Click);
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
            this.Controls.Add(this.cboTopoTarget);
            this.Controls.Add(this.cboTopoSource);
            this.Controls.Add(this.Button_OK);
            this.Controls.Add(this.Label2);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FormResetTopo";
            this.Text = "Reset Topo";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button Button_OK;
        internal System.Windows.Forms.Label Label2;
        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.ComboBox cboTopoSource;
        internal System.Windows.Forms.ComboBox cboTopoTarget;
    }
}