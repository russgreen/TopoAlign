using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Services;

/// <summary>
/// Shows a progress window on its own STA thread while a long Revit operation runs on the Revit thread.
/// The Revit thread only sends messages (it never touches the window), and the window thread never calls the Revit API.
/// Use in a <c>using</c> block so the window always closes, and check <see cref="Cancelled"/> inside loops.
/// </summary>
public sealed class ProgressService : IDisposable
{
    private const long MinUpdateIntervalMs = 50;

    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ProgressViewModel _viewModel;
    private readonly string? _format;
    private readonly Stopwatch _sinceLastUpdate = Stopwatch.StartNew();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;

    private volatile bool _cancelled;
    private Dispatcher? _dispatcher;
    private ProgressWindow? _window;
    private double _value;
    private double _maximum;
    private bool _disposed;

    private ProgressService(string title, double maximum, string? format, bool cancelEnabled, int showDelayMs)
    {
        _maximum = maximum;
        _format = format;

        _viewModel = new ProgressViewModel(_messenger, cancelEnabled);
        _messenger.Register<ProgressService, ProgressCancelMessage>(this, static (r, _) => r._cancelled = true);

        _thread = new Thread(() => RunWindow(showDelayMs));
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Name = "TopoAlign progress window";
        _thread.Start();
        _ready.Wait();

        Send(new ProgressUpdateMessage
        {
            Title = title,
            Message = FormatMessage(),
            Value = 0,
            Maximum = maximum > 0 ? maximum : 1,
            IsIndeterminate = maximum <= 0,
        });
    }

    /// <summary>
    /// Starts a progress window.
    /// </summary>
    /// <param name="title">The window caption.</param>
    /// <param name="maximum">Number of steps, or 0 (or less) for an indeterminate bar.</param>
    /// <param name="format">Message shown as steps complete; <c>{0}</c> is the number done. Null for no message.</param>
    /// <param name="cancelEnabled">Whether the Cancel button is available.</param>
    /// <param name="showDelayMs">The window only appears if the operation is still running after this delay, so short jobs do not flash it.</param>
    public static ProgressService Start(string title, double maximum = 0, string? format = null, bool cancelEnabled = true, int showDelayMs = 400)
        => new(title, maximum, format, cancelEnabled, showDelayMs);

    /// <summary>True once the user has pressed Cancel. Loops should stop (and roll back) when this becomes true.</summary>
    public bool Cancelled => _cancelled;

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> if the user pressed Cancel. Call it inside loops; open
    /// transactions are rolled back as the exception unwinds, and commands turn it into <c>Result.Cancelled</c>.
    /// </summary>
    public void ThrowIfCancelled()
    {
        if (_cancelled) throw new OperationCanceledException("Cancelled by user.");
    }

    /// <summary>Advances the bar by one step.</summary>
    public void Increment() => SetValue(_value + 1);

    /// <summary>Sets the number of steps done.</summary>
    public void SetValue(double value)
    {
        _value = value;

        // Throttle: a loop over many thousands of points must not flood the window thread.
        bool last = _maximum > 0 && value >= _maximum;
        if (!last && _sinceLastUpdate.ElapsedMilliseconds < MinUpdateIntervalMs) return;

        _sinceLastUpdate.Restart();
        Send(new ProgressUpdateMessage { Value = value, Message = FormatMessage() });
    }

    /// <summary>Changes the message text (for example the current stage).</summary>
    public void Report(string message) => Send(new ProgressUpdateMessage { Message = message });

    /// <summary>Switches to a determinate bar with a new step count, resetting the value.</summary>
    public void Restart(double maximum, string? message = null)
    {
        _maximum = maximum;
        _value = 0;
        Send(new ProgressUpdateMessage { Value = 0, Maximum = maximum, IsIndeterminate = false, Message = message ?? FormatMessage() });
    }

    /// <summary>Switches to a busy bar for a step with no countable loop.</summary>
    public void SetIndeterminate(string message)
        => Send(new ProgressUpdateMessage { IsIndeterminate = true, Message = message });

    private string? FormatMessage()
        => _format is null ? null : string.Format(_format, (int)_value);

    private void Send(ProgressUpdateMessage message)
    {
        if (!_disposed) _messenger.Send(message);
    }

    private void RunWindow(int showDelayMs)
    {
        try
        {
            _window = new ProgressWindow(_viewModel);
            _dispatcher = Dispatcher.CurrentDispatcher;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, showDelayMs)) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _window?.Show();
            };
            timer.Start();

            _ready.Set();
            Dispatcher.Run();
        }
        catch (Exception)
        {
            // The progress window is cosmetic: never let it take Revit down.
        }
        finally
        {
            _ready.Set();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _messenger.UnregisterAll(this);
        _messenger.UnregisterAll(_viewModel);

        try
        {
            var dispatcher = _dispatcher;
            if (dispatcher is not null)
            {
                dispatcher.BeginInvoke(() =>
                {
                    _window?.Close();
                    dispatcher.InvokeShutdown();
                });
                _thread.Join(2000);
            }
        }
        catch (Exception)
        {
            // Ignore: see RunWindow.
        }

        _ready.Dispose();
    }
}
