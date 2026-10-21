namespace MossAgent.Tools.Abstractions;

public interface ITaskWorkspaceLeaseProvider : ITaskWorkspaceGuard
{
    Task<ITaskWorkspaceLease> AcquireAsync(ToolExecutionContext context, CancellationToken cancellationToken);
}
