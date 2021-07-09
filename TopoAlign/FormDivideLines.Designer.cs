
namespace TopoAlign
{
    partial class FormDivideLines
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
            this.DisplayUnitTypecomboBox = new System.Windows.Forms.ComboBox();
            this.DisplayUnitcomboBox = new System.Windows.Forms.ComboBox();
            this.nudDivide = new System.Windows.Forms.NumericUpDown();
            this.lblUnits = new System.Windows.Forms.Label();
            this.Label1 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.nudVertOffset = new System.Windows.Forms.NumericUpDown();
            this.LabelVerticalOffset = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.nudDivide)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVertOffset)).BeginInit();
            this.SuspendLayout();
            // 
            // DisplayUnitTypecomboBox
            // 
            this.DisplayUnitTypecomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DisplayUnitTypecomboBox.FormattingEnabled = true;
            this.DisplayUnitTypecomboBox.Location = new System.Drawing.Point(91, 6);
            this.DisplayUnitTypecomboBox.Name = "DisplayUnitTypecomboBox";
            this.DisplayUnitTypecomboBox.Size = new System.Drawing.Size(126, 21);
            this.DisplayUnitTypecomboBox.TabIndex = 22;
            this.DisplayUnitTypecomboBox.Visible = false;
            // 
            // DisplayUnitcomboBox
            // 
            this.DisplayUnitcomboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DisplayUnitcomboBox.FormattingEnabled = true;
            this.DisplayUnitcomboBox.Location = new System.Drawing.Point(15, 25);
            this.DisplayUnitcomboBox.Name = "DisplayUnitcomboBox";
            this.DisplayUnitcomboBox.Size = new System.Drawing.Size(201, 21);
            this.DisplayUnitcomboBox.TabIndex = 21;
            this.DisplayUnitcomboBox.SelectedIndexChanged += new System.EventHandler(this.DisplayUnitcomboBox_SelectedIndexChanged);
            // 
            // nudDivide
            // 
            this.nudDivide.DecimalPlaces = 2;
            this.nudDivide.Location = new System.Drawing.Point(127, 52);
            this.nudDivide.Maximum = new decimal(new int[] {
            1000000000,
            0,
            0,
            0});
            this.nudDivide.Name = "nudDivide";
            this.nudDivide.Size = new System.Drawing.Size(90, 20);
            this.nudDivide.TabIndex = 20;
            this.nudDivide.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudDivide.Value = new decimal(new int[] {
            5000,
            0,
            0,
            0});
            // 
            // lblUnits
            // 
            this.lblUnits.AutoSize = true;
            this.lblUnits.Location = new System.Drawing.Point(12, 9);
            this.lblUnits.Name = "lblUnits";
            this.lblUnits.Size = new System.Drawing.Size(57, 13);
            this.lblUnits.TabIndex = 18;
            this.lblUnits.Text = "Units used";
            // 
            // Label1
            // 
            this.Label1.AutoSize = true;
            this.Label1.Location = new System.Drawing.Point(10, 54);
            this.Label1.Name = "Label1";
            this.Label1.Size = new System.Drawing.Size(107, 13);
            this.Label1.TabIndex = 19;
            this.Label1.Text = "Divide edge distance";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(15, 107);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(201, 56);
            this.btnOK.TabIndex = 23;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // nudVertOffset
            // 
            this.nudVertOffset.DecimalPlaces = 2;
            this.nudVertOffset.Location = new System.Drawing.Point(127, 78);
            this.nudVertOffset.Maximum = new decimal(new int[] {
            1000000000,
            0,
            0,
            0});
            this.nudVertOffset.Name = "nudVertOffset";
            this.nudVertOffset.Size = new System.Drawing.Size(89, 20);
            this.nudVertOffset.TabIndex = 25;
            this.nudVertOffset.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.nudVertOffset.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
            // 
            // LabelVerticalOffset
            // 
            this.LabelVerticalOffset.AutoSize = true;
            this.LabelVerticalOffset.Location = new System.Drawing.Point(13, 80);
            this.LabelVerticalOffset.Name = "LabelVerticalOffset";
            this.LabelVerticalOffset.Size = new System.Drawing.Size(71, 13);
            this.LabelVerticalOffset.TabIndex = 24;
            this.LabelVerticalOffset.Text = "Vertical offset";
            // 
            // FormDivideLines
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(228, 175);
            this.Controls.Add(this.nudVertOffset);
            this.Controls.Add(this.LabelVerticalOffset);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.DisplayUnitTypecomboBox);
            this.Controls.Add(this.DisplayUnitcomboBox);
            this.Controls.Add(this.nudDivide);
            this.Controls.Add(this.lblUnits);
            this.Controls.Add(this.Label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "FormDivideLines";
            this.Text = "Divide Lines";
            ((System.ComponentModel.ISupportInitialize)(this.nudDivide)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVertOffset)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.ComboBox DisplayUnitTypecomboBox;
        internal System.Windows.Forms.ComboBox DisplayUnitcomboBox;
        internal System.Windows.Forms.NumericUpDown nudDivide;
        internal System.Windows.Forms.Label lblUnits;
        internal System.Windows.Forms.Label Label1;
        private System.Windows.Forms.Button btnOK;
        internal System.Windows.Forms.NumericUpDown nudVertOffset;
        internal System.Windows.Forms.Label LabelVerticalOffset;
    }
}