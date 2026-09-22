namespace MossAgent.Tools.Abstractions;

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}
