using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TopoAlign.Services;

namespace TopoAlign.ViewModels;

/// <summary>
/// Drives the progress window. It listens for <see cref="ProgressUpdateMessage"/> on the
/// messenger it is given and sends <see cref="ProgressCancelMessage"/> when Cancel is pressed.
/// </summary>
internal sealed partial class ProgressViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly DateTime _startTime = DateTime.Now;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _timeMessage = string.Empty;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private double _maximum = 100;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private bool _cancelEnabled = true;

    public ProgressViewModel(IMessenger messenger, bool cancelEnabled)
    {
        _messenger = messenger;
        _cancelEnabled = cancelEnabled;

        _messenger.Register<ProgressViewModel, ProgressUpdateMessage>(this, static (r, m) => r.Apply(m));
    }

    private void Apply(ProgressUpdateMessage m)
    {
        if (m.Title is not null) Title = m.Title;
        if (m.Message is not null) Message = m.Message;
        if (m.Maximum is not null) Maximum = m.Maximum.Value;
        if (m.IsIndeterminate is not null) IsIndeterminate = m.IsIndeterminate.Value;

        if (m.Value is not null)
        {
            Value = m.Value.Value;
            UpdateTime();
        }
    }

    private void UpdateTime()
    {
        if (IsIndeterminate || Maximum <= 0 || Value <= 0)
        {
            TimeMessage = string.Empty;
            return;
        }

        var elapsed = DateTime.Now - _startTime;
        var overall = TimeSpan.FromTicks((long)(elapsed.Ticks / (Value / Maximum)));
        var remaining = overall - elapsed;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        TimeMessage = $"Elapsed {elapsed.Minutes:D2}:{elapsed.Seconds:D2}   Remaining {remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelEnabled = false;
        Message = "Cancelling...";
        _messenger.Send(new ProgressCancelMessage());
    }
}
