using System.Text.Json;

namespace CodingAgent.Domain.Models;

/// <summary>LLM 返回的工具调用（参数为原始 JSON 字符串）。</summary>
public class LlmToolCall
{
    public LlmToolCall() { }

    public LlmToolCall(string id, string name, string argumentsJson)
    {
        Id = id;
        Name = name;
        ArgumentsJson = argumentsJson;
    }

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";

    /// <summary>安全解析参数为 JsonElement，失败时返回空对象。</summary>
    public JsonElement ParseArguments()
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(ArgumentsJson) ? "{}" : ArgumentsJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}
