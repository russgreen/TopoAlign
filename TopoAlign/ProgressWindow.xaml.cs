using System.Windows;

namespace TopoAlign;
/// <summary>
/// Interaction logic for ProgressWindow.xaml
/// </summary>
public partial class ProgressWindow : Window, IDisposable
{
    private string _format;
    private bool _abortFlag = false;

    public ProgressWindow()
    {
        InitializeComponent();
    }

    public ProgressWindow(string caption, string format, int max)
    {
        InitializeComponent();
        _format = format;

        this.CaptionLanel.Text = caption;
        this.ProgressLabel.Text = format is null ? String.Empty : string.Format(format, 0); ;

        this.Progress.Minimum = 0;
        this.Progress.Maximum = max;
        this.Progress.Value = 0;

        Show();

        System.Windows.Forms.Application.DoEvents();
    }

    public void Increment()
    {
        this.Progress.Value++;

        if (_format is not null)
        {
            this.ProgressLabel.Text = string.Format(_format, this.Progress.Value);
        }

        System.Windows.Forms.Application.DoEvents();
    }

    public void SetCaption(string caption)
    {
        this.CaptionLanel.Text = caption;

        System.Windows.Forms.Application.DoEvents();
    }

    public bool GetAbortFlag()
    {
        return _abortFlag;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        this.AbortButton.Content = "Cancelling...";
        _abortFlag = true;
    }

    public void Dispose()
    {
        this.Close();
    }
}
