namespace MossAgent.Application.Automations;

internal sealed class AutomationSchedulerOperation : IDisposable
{
    private readonly CancellationTokenSource _cancellation;
    private readonly CancellationToken _caller;
    private Action? _release;

    public AutomationSchedulerOperation(CancellationToken shutdown, CancellationToken caller, Action release)
    {
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown, caller);
        _caller = caller;
        _release = release;
    }

    public async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        try { await operation(_cancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_caller.IsCancellationRequested)
        { _caller.ThrowIfCancellationRequested(); throw; }
    }

    public async Task<TResult> RunAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
    {
        try { return await operation(_cancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (_caller.IsCancellationRequested)
        { _caller.ThrowIfCancellationRequested(); throw; }
    }

    public void Dispose()
    {
        var release = Interlocked.Exchange(ref _release, null);
        if (release is null) return;
        _cancellation.Dispose();
        release();
    }
}
