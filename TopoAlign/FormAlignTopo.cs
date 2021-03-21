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
    public partial class FormAlignTopo : Form
 {
        private bool _oldVal;
   
        public FormAlignTopo()
        {
            InitializeComponent();
        }

        private void btnAlign_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        private void rdoElem_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoElem.Checked == true)
            {
                gbFaceToUse.Enabled = true;
            }
            else
            {
                gbFaceToUse.Enabled = false;
            }
        }

        private void rdoEdge_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoEdge.Checked == true)
            {
                _oldVal = chkRemoveInside.Checked;
                chkRemoveInside.Checked = false;
                chkRemoveInside.Enabled = false;
            }
            else
            {
                chkRemoveInside.Enabled = true;
                chkRemoveInside.Checked = _oldVal;
            }
        }

        private void DisplayUnitcomboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            DisplayUnitTypecomboBox.SelectedIndex = DisplayUnitcomboBox.SelectedIndex;
        }
    }
}
