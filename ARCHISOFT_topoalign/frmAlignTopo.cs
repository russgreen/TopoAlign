using System;

namespace ARCHISOFT_topoalign
{
    public partial class frmAlignTopo
    {
        public frmAlignTopo()
        {
            InitializeComponent();
            _btnAlign.Name = "btnAlign";
            _rdoEdge.Name = "rdoEdge";
            _rdoElem.Name = "rdoElem";
            _DisplayUnitcomboBox.Name = "DisplayUnitcomboBox";
        }

        private bool OldVal;

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
                OldVal = chkRemoveInside.Checked;
                chkRemoveInside.Checked = false;
                chkRemoveInside.Enabled = false;
            }
            else
            {
                chkRemoveInside.Enabled = true;
                chkRemoveInside.Checked = OldVal;
            }
        }

        private void frmAlignTopo_Load(object sender, EventArgs e)
        {
        }

        private void DisplayUnitcomboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            DisplayUnitTypecomboBox.SelectedIndex = DisplayUnitcomboBox.SelectedIndex;
        }
    }
}