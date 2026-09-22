namespace MossAgent.Tools.Abstractions;

public interface IToolExecutionAuditReader
{
    Task<IReadOnlyList<ToolExecutionAuditRecord>> GetRecentAsync(
        Guid taskId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolArtifactRecord>> GetArtifactsAsync(
        Guid executionId,
        CancellationToken cancellationToken);
}
