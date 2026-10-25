using System.Collections.Concurrent;
using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationProbeRunner : IAutomationTaskRunner
{
    public ConcurrentQueue<(AutomationSchedule Schedule, Guid TaskId)> Requests { get; } = new();
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<AutomationRunStatus> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Block { get; set; }
    public bool Throw { get; set; }

    public async Task<AutomationRunOutcome> RunAsync(AutomationRunContext context, Guid taskId, CancellationToken cancellationToken)
    {
        Requests.Enqueue((context.Schedule, taskId));
        Started.TrySetResult();
        if (Throw) throw new InvalidOperationException("synthetic-api-key-must-not-leak");
        return new(Block ? await Finish.Task.WaitAsync(cancellationToken) : AutomationRunStatus.Completed);
    }
}
