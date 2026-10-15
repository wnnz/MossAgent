using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class SelectiveToolApprovalService(
    Func<ToolDescriptor, bool> isApproved) : IToolApprovalService
{
    public ValueTask<bool> IsApprovedAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(isApproved(descriptor));
}
