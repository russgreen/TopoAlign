using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TopoAlign;

public partial class FormDivideLines : Form
{
    public FormDivideLines()
    {
        InitializeComponent();
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        DialogResult = System.Windows.Forms.DialogResult.OK;
        Close();
    }

    private void DisplayUnitcomboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        DisplayUnitTypecomboBox.SelectedIndex = DisplayUnitcomboBox.SelectedIndex;
    }
}
