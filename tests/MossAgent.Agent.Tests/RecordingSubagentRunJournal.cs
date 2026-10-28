using MossAgent.Application.Persistence;

namespace MossAgent.Agent.Tests;

/// <summary>
/// In-memory <see cref="ISubagentRunJournal"/> double. <see cref="BeginResult"/> false mimics
/// a missing parent task row; <see cref="BeginFailure"/>/<see cref="CompleteFailure"/> mimic
/// storage outages. Mutations are locked so concurrent delegations can be journaled safely.
/// </summary>
internal sealed class RecordingSubagentRunJournal : ISubagentRunJournal
{
    private readonly Lock _gate = new();
    public bool BeginResult { get; set; }
    public Exception? BeginFailure { get; set; }
    public Exception? CompleteFailure { get; set; }
    public List<SubagentRunRecord> Began { get; } = [];
    public List<(Guid RunId, SubagentRunStatus Status, string? ErrorCode)> Completed { get; } = [];
    public int InterruptedCalls { get; private set; }

    public Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken)
    {
        if (BeginFailure is not null) throw BeginFailure;
        if (BeginResult)
        {
            lock (_gate) Began.Add(record);
        }
        return Task.FromResult(BeginResult);
    }

    public Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken)
    {
        if (CompleteFailure is not null) throw CompleteFailure;
        lock (_gate) Completed.Add((runId, status, errorCode));
        return Task.CompletedTask;
    }

    public Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken)
    {
        lock (_gate) InterruptedCalls++;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<SubagentRunRecord>>(
                Began.Where(item => item.TaskId == taskId).ToArray());
    }
}
