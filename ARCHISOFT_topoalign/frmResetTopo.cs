using System;

namespace ARCHISOFT_topoalign
{
    public partial class frmResetTopo
    {
        public frmResetTopo()
        {
            InitializeComponent();
            _Button1.Name = "Button1";
            _cboTopoSource.Name = "cboTopoSource";
            _cboTopoTarget.Name = "cboTopoTarget";
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        private void cboTopoSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboTopoSource.Items.Count > 0 & cboTopoTarget.Items.Count > 0)
            {
                Button1.Enabled = true;
            }
        }
    }
}