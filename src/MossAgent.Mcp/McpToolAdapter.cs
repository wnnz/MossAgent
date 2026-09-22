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
        var result = await client.CallToolAsync(remoteName, request.Arguments, cancellationToken);
        return result.IsError
            ? ToolResult.Failure(result.Content, "mcp_tool_error")
            : ToolResult.Success($"MCP 工具 {remoteName} 执行完成。", result.Content);
    }
}

