using MossAgent.Application.Persistence;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeSubagentRunJournal : ISubagentRunJournal
{
    public Func<Guid, CancellationToken, Task<IReadOnlyList<SubagentRunRecord>>> Reads { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<SubagentRunRecord>>([]);
    public Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        Reads(taskId, cancellationToken);
}
