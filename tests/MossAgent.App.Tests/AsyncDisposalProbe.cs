using Avalonia.Threading;

namespace MossAgent.App.Tests;

internal sealed class AsyncDisposalProbe : IAsyncDisposable
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Started { get; private set; }
    public bool DispatcherTouched { get; private set; }
    public bool Disposed { get; private set; }

    public async ValueTask DisposeAsync()
    {
        Started = true;
        await _gate.Task.ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() => { DispatcherTouched = true; });
        Disposed = true;
    }

    public void Release() => _gate.TrySetResult();
}
