namespace MossAgent.Tools.Abstractions;

public interface ITaskWorkspaceGuard
{
    Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken);
}
