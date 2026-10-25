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
        McpCallResult result;
        try
        {
            result = await client.CallToolAsync(remoteName, request.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (McpReconnectingException)
        {
            return ToolResult.Failure("MCP 服务器正在重新连接，请稍后重试。", "mcp_reconnecting");
        }

        return result.IsError
            ? ToolResult.Failure("MCP 工具执行失败，请检查 MCP 服务状态或参数。", "mcp_tool_error")
            : ToolResult.Success($"MCP 工具 {remoteName} 执行完成。", result.Content);
    }
}
