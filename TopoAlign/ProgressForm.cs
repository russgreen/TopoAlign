namespace TopoAlign;

public partial class ProgressForm : Form
{
    private string _format;
    private bool abortFlag = false;

    public ProgressForm(string caption, string format, int max)
    {
        InitializeComponent();

        _format = format;
        Text = caption;
        Label1.Text = format is null ? caption : string.Format(format, 0);
        ProgressBar1.Minimum = 0;
        ProgressBar1.Maximum = max;
        ProgressBar1.Value = 0;

        Show();

        System.Windows.Forms.Application.DoEvents();
    }

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
        Button1.Text = "Aborting...";
        abortFlag = true;
    }
}
