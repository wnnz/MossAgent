using MossAgent.Application.Persistence;

namespace MossAgent.Agent.Tests;

/// <summary>
/// In-memory <see cref="ISubagentRunJournal"/> double. <see cref="BeginResult"/> false mimics
/// a missing parent task row; <see cref="BeginFailure"/>/<see cref="CompleteFailure"/> mimic
/// storage outages.
/// </summary>
internal sealed class RecordingSubagentRunJournal : ISubagentRunJournal
{
    public bool BeginResult { get; set; }
    public Exception? BeginFailure { get; set; }
    public Exception? CompleteFailure { get; set; }
    public List<SubagentRunRecord> Began { get; } = [];
    public List<(Guid RunId, SubagentRunStatus Status, string? ErrorCode)> Completed { get; } = [];
    public int InterruptedCalls { get; private set; }

    public Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken)
    {
        if (BeginFailure is not null) throw BeginFailure;
        if (BeginResult) Began.Add(record);
        return Task.FromResult(BeginResult);
    }

    public Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken)
    {
        if (CompleteFailure is not null) throw CompleteFailure;
        Completed.Add((runId, status, errorCode));
        return Task.CompletedTask;
    }

    public Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken)
    {
        InterruptedCalls++;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SubagentRunRecord>>(Began.Where(item => item.TaskId == taskId).ToArray());
}
