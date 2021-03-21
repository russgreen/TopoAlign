using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ARCHISOFT_topoalign
{
    [Microsoft.VisualBasic.CompilerServices.DesignerGenerated()]
    public partial class ProgressForm : System.Windows.Forms.Form
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
            ProgressBar1 = new System.Windows.Forms.ProgressBar();
            _btnAbort = new System.Windows.Forms.Button();
            _btnAbort.Click += new EventHandler(btnAbort_Click);
            SuspendLayout();
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(8, 7);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(68, 13);
            Label1.TabIndex = 1;
            Label1.Text = "Processing...";
            // 
            // ProgressBar1
            // 
            ProgressBar1.Location = new System.Drawing.Point(11, 25);
            ProgressBar1.Name = "ProgressBar1";
            ProgressBar1.Size = new System.Drawing.Size(318, 23);
            ProgressBar1.TabIndex = 0;
            // 
            // btnAbort
            // 
            _btnAbort.Location = new System.Drawing.Point(342, 25);
            _btnAbort.Name = "_btnAbort";
            _btnAbort.Size = new System.Drawing.Size(75, 23);
            _btnAbort.TabIndex = 2;
            _btnAbort.Text = "Abort";
            _btnAbort.UseVisualStyleBackColor = true;
            // 
            // ProgressForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(429, 61);
            ControlBox = false;
            Controls.Add(_btnAbort);
            Controls.Add(Label1);
            Controls.Add(ProgressBar1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ProgressForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Working...";
            TopMost = true;
            ResumeLayout(false);
            PerformLayout();
        }

        internal System.Windows.Forms.Label Label1;
        internal System.Windows.Forms.ProgressBar ProgressBar1;
        private System.Windows.Forms.Button _btnAbort;

        internal System.Windows.Forms.Button btnAbort
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _btnAbort;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_btnAbort != null)
                {
                    _btnAbort.Click -= btnAbort_Click;
                }

                _btnAbort = value;
                if (_btnAbort != null)
                {
                    _btnAbort.Click += btnAbort_Click;
                }
            }
        }
    }
}