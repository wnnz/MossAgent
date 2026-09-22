using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Mcp;

/// <summary>把已连接 MCP 服务器的工具桥接为 ITool（命名 mcp__<server>__<tool>）。</summary>
public class McpToolBridge(IMcpRepository mcpRepository, McpConnectionRegistry registry)
{
    public async Task<List<ITool>> GetConnectedToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ITool>();
        var servers = await mcpRepository.GetAllServersAsync(ct);
        foreach (var server in servers.Where(s => s.Enabled))
        {
            var connection = registry.Get(server.Id);
            if (connection is null || !connection.IsConnected)
            {
                continue;
            }
            var cachedTools = await mcpRepository.GetToolsAsync(server.Id, ct);
            foreach (var tool in cachedTools)
            {
                tools.Add(new McpBridgedTool(connection, server, tool));
            }
        }
        return tools;
    }
}

/// <summary>桥接单个 MCP 工具为 ITool。</summary>
public class McpBridgedTool(IMcpConnection connection, McpServer server, McpTool tool) : ITool
{
    public string Name => $"mcp__{server.Name}__{tool.Name}";
    public string Description => string.IsNullOrWhiteSpace(tool.Description)
        ? $"MCP 工具 {server.Name}/{tool.Name}"
        : tool.Description;
    public string ParametersSchemaJson => tool.SchemaJson;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var output = await connection.CallToolAsync(tool.Name, argumentsJson, ct);
            return ToolResult.Ok(output);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ToolResult.Fail($"MCP 工具调用失败: {ex.Message}");
        }
    }
}
