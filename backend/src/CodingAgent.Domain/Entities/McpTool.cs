namespace CodingAgent.Domain.Entities;

/// <summary>MCP 工具缓存（tools/list 结果）。</summary>
public class McpTool
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";
}
