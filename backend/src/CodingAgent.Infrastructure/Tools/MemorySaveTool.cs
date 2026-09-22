using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Tools;

public class MemorySaveTool(IMemoryRepository memories) : ITool
{
    public string Name => "memory_save";
    public string Description => "保存记忆条目。参数: {\"scope\": \"global|project\", \"title\": \"标题\", \"content\": \"内容\", \"tags\": \"可选，逗号分隔\"}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"scope":{"type":"string","enum":["global","project"]},"title":{"type":"string"},"content":{"type":"string"},"tags":{"type":"string"}},"required":["scope","title","content"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var title = ToolArguments.GetString(argumentsJson, "title");
        var content = ToolArguments.GetString(argumentsJson, "content");
        if (title.Length == 0 || content.Length == 0)
        {
            return ToolResult.Fail("title 和 content 不能为空");
        }
        var scopeText = ToolArguments.GetString(argumentsJson, "scope", "global");
        var scope = scopeText.Equals("project", StringComparison.OrdinalIgnoreCase) ? MemoryScope.Project : MemoryScope.Global;
        var tags = ToolArguments.GetString(argumentsJson, "tags");

        await memories.AddAsync(new MemoryItem { Scope = scope, Title = title, Content = content, Tags = string.IsNullOrWhiteSpace(tags) ? null : tags }, ct);
        return ToolResult.Ok($"已保存记忆: [{scopeText}] {title}");
    }
}
