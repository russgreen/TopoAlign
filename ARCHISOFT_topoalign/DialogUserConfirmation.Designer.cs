using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;

namespace ARCHISOFT_topoalign
{
    [DesignerGenerated()]
    public partial class DialogUserConfirmation : System.Windows.Forms.Form
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
            TableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            _OK_Button = new System.Windows.Forms.Button();
            _OK_Button.Click += new EventHandler(OK_Button_Click);
            _Cancel_Button = new System.Windows.Forms.Button();
            _Cancel_Button.Click += new EventHandler(Cancel_Button_Click);
            Label1 = new System.Windows.Forms.Label();
            TableLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // TableLayoutPanel1
            // 
            TableLayoutPanel1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            TableLayoutPanel1.ColumnCount = 2;
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0f));
            TableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0f));
            TableLayoutPanel1.Controls.Add(_OK_Button, 0, 0);
            TableLayoutPanel1.Controls.Add(_Cancel_Button, 1, 0);
            TableLayoutPanel1.Location = new System.Drawing.Point(230, 46);
            TableLayoutPanel1.Name = "TableLayoutPanel1";
            TableLayoutPanel1.RowCount = 1;
            TableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0f));
            TableLayoutPanel1.Size = new System.Drawing.Size(146, 29);
            TableLayoutPanel1.TabIndex = 0;
            // 
            // OK_Button
            // 
            _OK_Button.Anchor = System.Windows.Forms.AnchorStyles.None;
            _OK_Button.Location = new System.Drawing.Point(3, 3);
            _OK_Button.Name = "_OK_Button";
            _OK_Button.Size = new System.Drawing.Size(67, 23);
            _OK_Button.TabIndex = 0;
            _OK_Button.Text = "Send";
            // 
            // Cancel_Button
            // 
            _Cancel_Button.Anchor = System.Windows.Forms.AnchorStyles.None;
            _Cancel_Button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            _Cancel_Button.Location = new System.Drawing.Point(76, 3);
            _Cancel_Button.Name = "_Cancel_Button";
            _Cancel_Button.Size = new System.Drawing.Size(67, 23);
            _Cancel_Button.TabIndex = 1;
            _Cancel_Button.Text = "Don't send";
            // 
            // Label1
            // 
            Label1.AutoSize = true;
            Label1.Location = new System.Drawing.Point(13, 13);
            Label1.Name = "Label1";
            Label1.Size = new System.Drawing.Size(346, 13);
            Label1.TabIndex = 1;
            Label1.Text = "Would you like to send an anonymous report so we can fix the problem?";
            // 
            // Dialog1
            // 
            AcceptButton = _OK_Button;
            AutoScaleDimensions = new System.Drawing.SizeF(6.0f, 13.0f);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = _Cancel_Button;
            ClientSize = new System.Drawing.Size(388, 87);
            Controls.Add(Label1);
            Controls.Add(TableLayoutPanel1);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Dialog1";
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Unexpected crash found";
            TableLayoutPanel1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        internal System.Windows.Forms.TableLayoutPanel TableLayoutPanel1;
        private System.Windows.Forms.Button _OK_Button;

        internal System.Windows.Forms.Button OK_Button
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _OK_Button;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_OK_Button != null)
                {
                    _OK_Button.Click -= OK_Button_Click;
                }

                _OK_Button = value;
                if (_OK_Button != null)
                {
                    _OK_Button.Click += OK_Button_Click;
                }
            }
        }

        private System.Windows.Forms.Button _Cancel_Button;

        internal System.Windows.Forms.Button Cancel_Button
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                return _Cancel_Button;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                if (_Cancel_Button != null)
                {
                    _Cancel_Button.Click -= Cancel_Button_Click;
                }

                _Cancel_Button = value;
                if (_Cancel_Button != null)
                {
                    _Cancel_Button.Click += Cancel_Button_Click;
                }
            }
        }

        internal System.Windows.Forms.Label Label1;
    }
}