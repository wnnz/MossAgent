namespace CodingAgent.Domain.Models;

/// <summary>MCP 工具信息（tools/list 结果）。</summary>
public class McpToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";
}
