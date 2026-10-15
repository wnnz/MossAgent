using MossAgent.Mcp;

namespace MossAgent.App.Tests;

internal sealed class DeferredStartupMcpManager : IMcpManager
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IReadOnlyDictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
    public Exception? Failure { get; set; }
    public int ReloadCount { get; private set; }
    public bool WasCancelled { get; private set; }
    public bool IsDisposed { get; private set; }
    public SynchronizationContext? ReloadContext { get; private set; }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ReloadCount++;
        ReloadContext = SynchronizationContext.Current;
        Started.TrySetResult();
        try { await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WasCancelled = true;
            throw;
        }
        if (Failure is not null) throw Failure;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
