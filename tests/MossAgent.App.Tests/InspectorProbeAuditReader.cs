using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeAuditReader : IToolExecutionAuditReader
{
    public Func<Guid, CancellationToken, Task<IReadOnlyList<ToolExecutionAuditRecord>>> Recent { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([]);
    public Func<Guid, CancellationToken, Task<IReadOnlyList<ToolArtifactRecord>>> Artifacts { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<ToolArtifactRecord>>([]);
    public Task<IReadOnlyList<ToolExecutionAuditRecord>> GetRecentAsync(Guid taskId, int limit, CancellationToken cancellationToken) => Recent(taskId, cancellationToken);
    public Task<IReadOnlyList<ToolArtifactRecord>> GetArtifactsAsync(Guid executionId, CancellationToken cancellationToken) => Artifacts(executionId, cancellationToken);
}
