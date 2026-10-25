using System.Collections.Concurrent;
using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationShutdownProbeRunner : IAutomationTaskRunner
{
    public bool Block { get; set; }
    public ConcurrentQueue<Guid> Requests { get; } = new();
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<AutomationRunOutcome> RunAsync(AutomationRunContext context, Guid taskId, CancellationToken cancellationToken)
    {
        Requests.Enqueue(taskId);
        using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
        Started.TrySetResult();
        if (Block) await Released.Task;
        return new(AutomationRunStatus.Completed);
    }
}
