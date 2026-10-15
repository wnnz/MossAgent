namespace MossAgent.Mcp;

internal sealed class McpReloadLifetime
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _operations;
    private bool _stopping;

    public McpReloadOperation Enter()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            _operations++;
            return new McpReloadOperation(_shutdown.Token, Release);
        }
    }

    public ValueTask StopAsync(Func<Task> cleanup)
    {
        bool start;
        lock (_gate)
        {
            start = !_stopping;
            _stopping = true;
            if (_operations == 0) _drained.TrySetResult();
        }
        if (start) _ = FinishAsync(cleanup);
        return new ValueTask(_stopped.Task);
    }

    private void Release()
    {
        lock (_gate)
        {
            _operations--;
            if (_stopping && _operations == 0) _drained.TrySetResult();
        }
    }

    private async Task FinishAsync(Func<Task> cleanup)
    {
        Exception? failure = null;
        try { await _shutdown.CancelAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        try
        {
            await _drained.Task.ConfigureAwait(false);
            await cleanup().ConfigureAwait(false);
        }
        catch (Exception exception) { failure ??= exception; }
        finally { _shutdown.Dispose(); }
        if (failure is null) _stopped.TrySetResult();
        else _stopped.TrySetException(failure);
    }
}
