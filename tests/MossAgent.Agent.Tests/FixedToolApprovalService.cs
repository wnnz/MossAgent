using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class FixedToolApprovalService(bool isApproved) : IToolApprovalService
{
    public ToolDescriptor? LastDescriptor { get; private set; }

    public ValueTask<bool> IsApprovedAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        LastDescriptor = descriptor;
        return ValueTask.FromResult(isApproved);
    }
}
