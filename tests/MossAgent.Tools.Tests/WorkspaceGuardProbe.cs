using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class WorkspaceGuardProbe : ITaskWorkspaceGuard
{
    public int Checks { get; private set; }
    public Func<int, CancellationToken, Task>? Handler { get; set; }

    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Checks++;
        return Handler?.Invoke(Checks, cancellationToken) ?? Task.CompletedTask;
    }
}
