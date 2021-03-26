using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TopoAlign
{
    public partial class FormResetTopo : Form
    {
        public FormResetTopo()
        {
            InitializeComponent();
        }

        private void cboTopoSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboTopoSource.Items.Count > 0 & cboTopoTarget.Items.Count > 0)
            {
                Button_OK.Enabled = true;
            }
        }

        private void Button_OK_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }
    }
}
