using MossAgent.Application.Automations;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationShutdownFixture : IAsyncDisposable
{
    private readonly List<Task> _operations = [];
    public AutomationShutdownFixture(string? phase = null, bool cooperative = true)
    {
        Repository = new AutomationShutdownProbeRepository { BlockAt = phase, IsCooperative = cooperative };
        Scheduler = new AutomationScheduler(Repository, Runner, Lease, TimeProvider.System);
    }
    public AutomationShutdownProbeRepository Repository { get; }
    public AutomationShutdownProbeRunner Runner { get; } = new();
    public AutomationShutdownProbeLease Lease { get; } = new();
    public AutomationScheduler Scheduler { get; }
    public Task Track(Task operation) { _operations.Add(operation); return operation; }
    public async ValueTask DisposeAsync()
    {
        Repository.Released.TrySetResult();
        Runner.Released.TrySetResult();
        await Scheduler.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await Task.WhenAll(_operations).WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
    }
}
