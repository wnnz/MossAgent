using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class SourceReviewApprovalProbe(Action action, bool approved = true) : IToolApprovalService
{
    public int Calls { get; private set; }
    public ValueTask<bool> IsApprovedAsync(ToolDescriptor descriptor, ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    { Calls++; action(); return ValueTask.FromResult(approved); }
}
