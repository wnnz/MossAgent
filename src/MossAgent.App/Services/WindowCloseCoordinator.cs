using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Services;

public sealed class WindowCloseCoordinator : IDisposable
{
    private readonly AppWindow _window;
    private readonly AppShutdownViewModel _state;
    private readonly Func<Task> _shutdown;
    private readonly Action<Action> _schedule;
    private readonly IClassicDesktopStyleApplicationLifetime? _desktop;
    private bool _closing;
    private bool _ready;
    private bool _disposed;

    public WindowCloseCoordinator(AppWindow window, AppShutdownViewModel state, Func<Task> shutdown,
        IClassicDesktopStyleApplicationLifetime? desktop = null, Action<Action>? schedule = null)
    {
        _window = window;
        _state = state;
        _shutdown = shutdown;
        _desktop = desktop;
        _schedule = schedule ?? (callback => Dispatcher.UIThread.Post(callback));
        window.Closing += HandleClosing;
        window.Closed += HandleClosed;
        state.ForceExitRequested += ForceExit;
        state.RetryExitRequested += RetryExit;
        if (desktop is not null) desktop.ShutdownRequested += HandleShutdownRequested;
    }

    private void HandleClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_disposed || _ready || args.Cancel) return;
        args.Cancel = true;
        if (_closing) return;
        _closing = true;
        _state.Begin();
        _ = FinishAsync();
    }

    private async Task FinishAsync()
    {
        try
        {
            await _shutdown();
            if (_disposed) return;
            _state.Complete();
            _ready = true;
            ScheduleClose();
        }
        catch (Exception)
        {
            if (!_disposed) _state.Fail();
        }
    }

    private void HandleShutdownRequested(object? sender, ShutdownRequestedEventArgs args)
    {
        if (_disposed || _ready || args.Cancel) return;
        args.Cancel = true;
        _window.Close();
    }

    private void ForceExit()
    {
        if (_disposed || _ready || !_state.HasError) return;
        _ready = true;
        ScheduleClose();
    }

    private void RetryExit()
    {
        if (_disposed || _ready || !_closing || !_state.HasError) return;
        _state.Begin();
        _ = FinishAsync();
    }

    private void ScheduleClose() => _schedule(() => { if (!_disposed) _window.Close(); });
    private void HandleClosed(object? sender, EventArgs args) => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Closing -= HandleClosing;
        _window.Closed -= HandleClosed;
        _state.ForceExitRequested -= ForceExit;
        _state.RetryExitRequested -= RetryExit;
        if (_desktop is not null) _desktop.ShutdownRequested -= HandleShutdownRequested;
    }
}
