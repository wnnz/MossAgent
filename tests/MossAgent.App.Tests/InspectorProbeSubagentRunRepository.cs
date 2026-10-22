using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeSubagentRunRepository : ISubagentRunRepository
{
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public Func<Guid, int, CancellationToken, Task<IReadOnlyList<SubagentRun>>> Recent { get; set; } =
        (_, _, _) => Task.FromResult<IReadOnlyList<SubagentRun>>([]);
    public Func<Guid, Guid, string, CancellationToken, Task<SubagentRun>> Interrupt { get; set; } =
        (_, _, _, _) => throw new NotSupportedException();

    public Task<IReadOnlyList<SubagentRun>> GetRecentAsync(Guid taskId, int limit,
        CancellationToken cancellationToken) => Recent(taskId, limit, cancellationToken);

    public Task<SubagentRun> StartAsync(SubagentRun run, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<SubagentRun> CompleteAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SubagentRun> FailAsync(Guid taskId, Guid runId, string summary, string errorCode,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SubagentRun> CancelAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<SubagentRun> MarkInterruptedAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) => Interrupt(taskId, runId, summary, cancellationToken);
}
