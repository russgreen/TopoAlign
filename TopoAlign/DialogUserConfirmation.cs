using Microsoft.AppCenter.Crashes;
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
    public partial class DialogUserConfirmation : Form
    {
        public UserConfirmation ClickResult { get; set; }

        public DialogUserConfirmation()
        {
            InitializeComponent();
        }

        private void OK_Button_Click(object sender, EventArgs e)
        {
            ClickResult = UserConfirmation.Send;
            this.DialogResult = DialogResult.None;
            this.Close();
        }

        private void Cancel_Button_Click(object sender, EventArgs e)
        {
            ClickResult = UserConfirmation.DontSend;
            this.DialogResult = DialogResult.None;
            this.Close();
        }
    }
}
