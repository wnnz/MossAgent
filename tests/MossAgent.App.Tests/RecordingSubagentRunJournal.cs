using MossAgent.Application.Persistence;

namespace MossAgent.App.Tests;

internal sealed class RecordingSubagentRunJournal : ISubagentRunJournal
{
    public Exception? ReconcileFailure { get; set; }
    public int ReconcileCalls { get; private set; }

    public Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        if (ReconcileFailure is not null) throw ReconcileFailure;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SubagentRunRecord>>([]);
}
