namespace MossAgent.Browser;

internal sealed class PlaywrightBrowserOperation(Func<ValueTask> closeSession)
{
    private int _stopped;

    public void Stop() => Interlocked.Exchange(ref _stopped, 1);

    public Task RunAsync(Func<Task> action, CancellationToken cancellationToken) =>
        RunAsync(async () => { await action().ConfigureAwait(false); return true; }, cancellationToken);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
        var pending = action();
        try
        {
            var result = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Stop();
            try { await closeSession().ConfigureAwait(false); }
            finally { await ((Task)pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
            throw;
        }
    }
}
