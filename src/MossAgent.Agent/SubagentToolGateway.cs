using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal sealed class SubagentToolGateway(
    IToolCatalog catalog,
    IToolExecutor executor) : ISubagentToolGateway
{
    public IReadOnlyList<ToolDescriptor> Descriptors =>
        catalog.Descriptors
            .Where(static descriptor => !descriptor.Name.Equals(
                SubagentTool.ToolName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (request.ToolName.Equals(SubagentTool.ToolName, StringComparison.OrdinalIgnoreCase)
            || !catalog.Descriptors.Any(descriptor => descriptor.Name.Equals(
                request.ToolName, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(ToolResult.Failure(
                "子代理不能调用此工具。", "subagent_tool_unavailable"));
        }

        var childContext = context with
        {
            ConversationContext = null,
            SubagentTools = null,
            SubagentProgress = null
        };
        return executor.ExecuteAsync(request, childContext, cancellationToken);
    }
}
