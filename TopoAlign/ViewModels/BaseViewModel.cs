using CommunityToolkit.Mvvm.ComponentModel;

namespace TopoAlign.ViewModels;

public class BaseViewModel : ObservableValidator
{
    public event EventHandler? ClosingRequest;

    /// <summary>Raised when the dialog should hide itself so the command can run an interactive pick.</summary>
    public event EventHandler? HideRequested;

    protected void OnClosingRequest()
    {
        ClosingRequest?.Invoke(this, EventArgs.Empty);
    }

    protected void OnHideRequested()
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }
}
