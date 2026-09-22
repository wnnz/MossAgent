namespace MossAgent.Tools.Abstractions;

public interface IToolApprovalService
{
    ValueTask<bool> IsApprovedAsync(
        ToolDescriptor descriptor,
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}

