using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Tools;

public class MemorySearchTool(IMemoryRepository memories) : ITool
{
    public string Name => "memory_search";
    public string Description => "按关键词检索记忆。参数: {\"query\": \"关键词\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var query = ToolArguments.GetString(argumentsJson, "query");
        if (query.Length == 0)
        {
            return ToolResult.Fail("query 不能为空");
        }
        var items = await memories.SearchAsync(query, ct);
        if (items.Count == 0)
        {
            return ToolResult.Ok("无匹配记忆");
        }
        var output = string.Join("\n---\n", items.Select(m =>
            $"[{m.Scope}] {m.Title}" + (string.IsNullOrWhiteSpace(m.Tags) ? "" : $" (tags: {m.Tags})") + $"\n{m.Content}"));
        return ToolResult.Ok(output);
    }
}
