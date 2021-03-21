using System;
using System.Windows.Forms;

namespace ARCHISOFT_topoalign
{
    public partial class DialogUserConfirmation
    {
        public DialogUserConfirmation()
        {
            InitializeComponent();
            _OK_Button.Name = "OK_Button";
            _Cancel_Button.Name = "Cancel_Button";
        }

        // Private _clickResult As UserConfirmation
        // Public Property ClickResult() As UserConfirmation
        // Get
        // Return _clickResult
        // End Get
        // Set(ByVal value As UserConfirmation)
        // _clickResult = value
        // End Set
        // End Property

        private void OK_Button_Click(object sender, EventArgs e)
        {
            // ClickResult = UserConfirmation.Send
            DialogResult = DialogResult.None;
            Close();
        }

        private void Cancel_Button_Click(object sender, EventArgs e)
        {
            // ClickResult = UserConfirmation.DontSend
            DialogResult = DialogResult.None;
            Close();
        }
    }
}