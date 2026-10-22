using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class InMemorySubagentRunRepository : ISubagentRunRepository
{
    private readonly Dictionary<Guid, SubagentRun> _runs = [];

    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<SubagentRun> Runs => _runs.Values.OrderBy(run => run.CreatedAt).ToArray();

    public Task<SubagentRun> StartAsync(SubagentRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        run = run with { OwnerInstanceId = InstanceId };
        if (run.Id == Guid.Empty || !_runs.TryAdd(run.Id, run))
            throw new InvalidOperationException("Duplicate subagent run.");
        return Task.FromResult(run);
    }

    public Task<SubagentRun> CompleteAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Completed, summary, null, cancellationToken);

    public Task<SubagentRun> FailAsync(Guid taskId, Guid runId, string summary, string errorCode,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Failed, summary, errorCode, cancellationToken);

    public Task<SubagentRun> CancelAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Cancelled, summary, "tool_cancelled", cancellationToken);

    public Task<SubagentRun> MarkInterruptedAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(runId, out var run) || run.TaskId != taskId
            || run.Status != SubagentRunStatus.Running
            || run.OwnerInstanceId.Equals(InstanceId, StringComparison.Ordinal))
            throw new InvalidOperationException("Subagent run cannot be marked interrupted.");
        return FinishAsync(taskId, runId, SubagentRunStatus.Interrupted, summary,
            "subagent_run_interrupted", cancellationToken);
    }

    public Task<IReadOnlyList<SubagentRun>> GetRecentAsync(Guid taskId, int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SubagentRun> result = _runs.Values
            .Where(run => run.TaskId == taskId)
            .OrderByDescending(run => run.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToArray();
        return Task.FromResult(result);
    }

    private Task<SubagentRun> FinishAsync(Guid taskId, Guid runId, SubagentRunStatus status,
        string summary, string? errorCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_runs.TryGetValue(runId, out var run) || run.TaskId != taskId)
            throw new InvalidOperationException("Subagent run does not exist.");
        if (run.Status != SubagentRunStatus.Running) return Task.FromResult(run);
        var now = DateTimeOffset.UtcNow;
        run = run with
        {
            Status = status,
            Summary = summary,
            ErrorCode = errorCode,
            UpdatedAt = now,
            CompletedAt = now
        };
        _runs[runId] = run;
        return Task.FromResult(run);
    }
}
