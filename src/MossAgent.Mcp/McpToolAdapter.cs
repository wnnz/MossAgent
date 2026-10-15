using MossAgent.Tools.Abstractions;

namespace MossAgent.Mcp;

public sealed class McpToolAdapter(
    string registeredName,
    string remoteName,
    McpToolDefinition definition,
    IMcpClient client) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        registeredName,
        definition.Description,
        definition.InputSchema.GetRawText(),
        definition.IsReadOnly ? ToolRiskLevel.ReadOnly : ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var result = await client.CallToolAsync(remoteName, request.Arguments, cancellationToken).ConfigureAwait(false);
        return result.IsError
            ? ToolResult.Failure("MCP 工具执行失败，请检查 MCP 服务状态或参数。", "mcp_tool_error")
            : ToolResult.Success($"MCP 工具 {remoteName} 执行完成。", result.Content);
    }
}
