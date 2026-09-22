namespace MossAgent.Tools.Abstractions;

public interface IToolExecutionAuditSink
{
    Task<Guid> StartAsync(
        ToolExecutionAuditStart entry,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid executionId,
        ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts,
        CancellationToken cancellationToken);
}
