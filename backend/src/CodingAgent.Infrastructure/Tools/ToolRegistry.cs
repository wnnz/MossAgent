using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Mcp;

namespace CodingAgent.Infrastructure.Tools;

/// <summary>工具注册表：静态内置工具 + 已连接 MCP 桥接工具。</summary>
public class ToolRegistry(IEnumerable<ITool> staticTools, McpToolBridge mcpBridge)
{
    private readonly ITool[] _staticTools = [.. staticTools];

    public async Task<List<ITool>> GetToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ITool>(_staticTools);
        tools.AddRange(await mcpBridge.GetConnectedToolsAsync(ct));
        return tools;
    }

    public async Task<List<LlmToolDefinition>> GetDefinitionsAsync(CancellationToken ct = default)
    {
        var tools = await GetToolsAsync(ct);
        return [.. tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.ParametersSchemaJson))];
    }
}
