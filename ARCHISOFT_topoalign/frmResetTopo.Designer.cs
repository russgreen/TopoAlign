using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ARCHISOFT_topoalign
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class frmResetTopo : System.Windows.Forms.Form
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
            Label1 = new System.Windows.Forms.Label();
            Label2 = new System.Windows.Forms.Label();
            _Button1 = new System.Windows.Forms.Button();
            _Button1.Click += new EventHandler(Button1_Click);
            _cboTopoSource = new System.Windows.Forms.ComboBox();
            _cboTopoSource.SelectedIndexChanged += new EventHandler(cboTopoSource_SelectedIndexChanged);
            _cboTopoTarget = new System.Windows.Forms.ComboBox();
            _cboTopoTarget.SelectedIndexChanged += new EventHandler(cboTopoSource_SelectedIndexChanged);
            SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(12, 14);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(103, 13);
            Label1.TabIndex = 1;
            Label1.Text = "Source topo surface";
            // 
            // Label2
            // 
            Label2.AutoSize = true;
            Label2.Location = new System.Drawing.Point(12, 62);
            Label2.Name = "Label2";
            Label2.Size = new System.Drawing.Size(100, 13);
            Label2.TabIndex = 2;
            Label2.Text = "Target topo surface";
            // 
            // Button1
            // 
            _Button1.Enabled = false;
            _Button1.Location = new System.Drawing.Point(131, 111);
            _Button1.Name = "_Button1";
            _Button1.Size = new System.Drawing.Size(197, 55);
            _Button1.TabIndex = 4;
            _Button1.Text = "Reset topo region";
            _Button1.UseVisualStyleBackColor = true;
            // 
            // cboTopoSource
            // 
            _cboTopoSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            _cboTopoSource.FormattingEnabled = true;
            _cboTopoSource.Location = new System.Drawing.Point(15, 30);
            _cboTopoSource.Name = "_cboTopoSource";
            _cboTopoSource.Size = new System.Drawing.Size(313, 21);
            _cboTopoSource.TabIndex = 5;
            // 
            // cboTopoTarget
            // 
            _cboTopoTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            _cboTopoTarget.FormattingEnabled = true;
            _cboTopoTarget.Location = new System.Drawing.Point(13, 78);
            _cboTopoTarget.Name = "_cboTopoTarget";
            _cboTopoTarget.Size = new System.Drawing.Size(315, 21);
            _cboTopoTarget.TabIndex = 6;
            // 
            // frmResetTopo
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(343, 178);
            Controls.Add(_cboTopoTarget);
            Controls.Add(_cboTopoSource);
            Controls.Add(_Button1);
            Controls.Add(Label2);
            Controls.Add(Label1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            Name = "frmResetTopo";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Reset Topo Region";
            ResumeLayout(false);
            PerformLayout();
        }

        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.Label Label2;
        private System.Windows.Forms.Button _Button1;

        internal System.Windows.Forms.Button Button1
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _Button1;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_Button1 != null)
                {
                    _Button1.Click -= Button1_Click;
                }

                _Button1 = value;
                if (_Button1 != null)
                {
                    _Button1.Click += Button1_Click;
                }
            }
        }

        private System.Windows.Forms.ComboBox _cboTopoSource;

        internal System.Windows.Forms.ComboBox cboTopoSource
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _cboTopoSource;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_cboTopoSource != null)
                {
                    _cboTopoSource.SelectedIndexChanged -= cboTopoSource_SelectedIndexChanged;
                }

                _cboTopoSource = value;
                if (_cboTopoSource != null)
                {
                    _cboTopoSource.SelectedIndexChanged += cboTopoSource_SelectedIndexChanged;
                }
            }
        }

        private System.Windows.Forms.ComboBox _cboTopoTarget;

        internal System.Windows.Forms.ComboBox cboTopoTarget
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _cboTopoTarget;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_cboTopoTarget != null)
                {
                    _cboTopoTarget.SelectedIndexChanged -= cboTopoSource_SelectedIndexChanged;
                }

                _cboTopoTarget = value;
                if (_cboTopoTarget != null)
                {
                    _cboTopoTarget.SelectedIndexChanged += cboTopoSource_SelectedIndexChanged;
                }
            }
        }
    }
}