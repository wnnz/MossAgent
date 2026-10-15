using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class PreviewDenyApprovalService : IToolApprovalService
{
    public ValueTask<bool> IsApprovedAsync(ToolDescriptor descriptor, ToolRequest request,
        ToolExecutionContext context, CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
