using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CapturingTerminalWorkspaceGuard(ITaskWorkspaceGuard inner) : ITaskWorkspaceLeaseProvider
{
    public ITaskWorkspaceLease? Lease { get; private set; }

    public Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken) =>
        inner.ValidateAsync(context, cancellationToken);

    public async Task<ITaskWorkspaceLease> AcquireAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        Lease = await ((ITaskWorkspaceLeaseProvider)inner).AcquireAsync(context, cancellationToken).ConfigureAwait(false);
        return Lease;
    }
}
