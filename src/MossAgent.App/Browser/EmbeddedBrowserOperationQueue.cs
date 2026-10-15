namespace MossAgent.App.Browser;

internal sealed class EmbeddedBrowserOperationQueue
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposing;

    public async Task<TResult> RunAsync<TResult>(
        Func<Task<TResult>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposing) != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposing) != 0, this);
            return await action().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public Task RunAsync(Func<Task> action, CancellationToken cancellationToken) =>
        RunAsync(async () => { await action().ConfigureAwait(false); return true; }, cancellationToken);

    public async ValueTask DisposeAsync(Func<Task> dispose)
    {
        if (Interlocked.CompareExchange(ref _disposing, 1, 0) != 0)
        {
            await _closed.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { await dispose().ConfigureAwait(false); }
            finally { _gate.Release(); }
            _closed.TrySetResult();
        }
        catch (Exception exception)
        {
            _closed.TrySetException(exception);
            throw;
        }
    }
}
