using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackCopyWorkspaceGuard(Action action) : ITaskWorkspaceGuard
{
    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        action();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
