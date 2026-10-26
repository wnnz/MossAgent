namespace MossAgent.Application.Persistence;

/// <summary>
/// Persists every delegated subagent run so a crash leaves a viewable terminal record
/// instead of a silent orphan. Beginning a run is fail-closed at the call site; terminal
/// transitions are best-effort and must never mask the run result.
/// </summary>
public interface ISubagentRunJournal
{
    /// <summary>
    /// Records a run as <see cref="SubagentRunStatus.Running"/> before the child model call.
    /// Returns false when the parent task row is absent or archived and no row was written.
    /// </summary>
    Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Transitions a running row to a terminal status; a no-op once any terminal status
    /// exists, so completion is idempotent.
    /// </summary>
    Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Startup reconciliation: marks leftover running rows interrupted. Never replays a run.
    /// </summary>
    Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken);

    /// <summary>Lists journaled runs for one task, oldest first.</summary>
    Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId, CancellationToken cancellationToken);
}
