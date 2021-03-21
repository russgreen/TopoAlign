using System;

namespace ARCHISOFT_topoalign
{
    public partial class ProgressForm : System.Windows.Forms.Form
    {

        /// <summary>
    /// Set up progress bar form and immediately display it modelessly.
    /// </summary>
    /// <param name="caption">Form caption</param>
    /// <param name="format">Progress message string</param>
    /// <param name="max">Number of elements to process</param>
        public ProgressForm(string caption, string format, int max)
        {
            _format = format;
            InitializeComponent();
            Text = caption;
            Label1.Text = format is null ? caption : string.Format(format, 0);
            ProgressBar1.Minimum = 0;
            ProgressBar1.Maximum = max;
            ProgressBar1.Value = 0;
            Show();
            System.Windows.Forms.Application.DoEvents();
            _btnAbort.Name = "btnAbort";
        }

        private string _format;
        private bool abortFlag = false;

        public void Increment()
        {
            ProgressBar1.Value += 1;
            if (_format is object)
            {
                Label1.Text = string.Format(_format, ProgressBar1.Value);
            }

            System.Windows.Forms.Application.DoEvents();
        }

        public bool getAbortFlag()
        {
            return abortFlag;
        }

        private void btnAbort_Click(object sender, EventArgs e)
        {
            btnAbort.Text = "Aborting...";
            abortFlag = true;
        }
    }
}