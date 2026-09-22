namespace MossAgent.Tools.Abstractions;

public interface IAgentTool
{
    ToolDescriptor Descriptor { get; }

    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}

