using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface ISubagentRunRepository
{
    string InstanceId { get; }

    Task<SubagentRun> StartAsync(SubagentRun run, CancellationToken cancellationToken);

    Task<SubagentRun> CompleteAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken);

    Task<SubagentRun> FailAsync(Guid taskId, Guid runId, string summary, string errorCode,
        CancellationToken cancellationToken);

    Task<SubagentRun> CancelAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken);

    Task<SubagentRun> MarkInterruptedAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubagentRun>> GetRecentAsync(Guid taskId, int limit,
        CancellationToken cancellationToken);
}
