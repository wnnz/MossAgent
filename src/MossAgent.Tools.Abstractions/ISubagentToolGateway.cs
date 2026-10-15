namespace MossAgent.Tools.Abstractions;

public interface ISubagentToolGateway
{
    IReadOnlyList<ToolDescriptor> Descriptors { get; }

    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}
