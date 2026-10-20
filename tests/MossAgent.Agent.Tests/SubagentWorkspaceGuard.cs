using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class SubagentWorkspaceGuard(int allowedChecks) : ITaskWorkspaceGuard
{
    public List<ToolExecutionContext> Contexts { get; } = [];

    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Contexts.Add(context);
        if (Contexts.Count > allowedChecks) throw new UnauthorizedAccessException("synthetic-workspace-secret");
        return Task.CompletedTask;
    }
}
