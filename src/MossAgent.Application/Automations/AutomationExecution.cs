namespace MossAgent.Application.Automations;

internal sealed class AutomationExecution : IDisposable
{
    private readonly CancellationTokenSource _timeout;
    private readonly CancellationTokenSource _cancellation;

    public AutomationExecution(int timeoutMinutes, TimeProvider clock, CancellationToken shutdown)
    {
        _timeout = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes), clock);
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown, _timeout.Token);
    }

    public Task Completion { get; set; } = Task.CompletedTask;
    public CancellationToken Token => _cancellation.Token;
    public bool IsTimedOut => _timeout.IsCancellationRequested;
    public void Cancel() => _cancellation.Cancel();

    public void Dispose()
    {
        _cancellation.Dispose();
        _timeout.Dispose();
    }
}
