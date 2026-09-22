using System.Text.Json;

namespace CodingAgent.Domain.Models;

/// <summary>注册给 LLM 的工具定义。</summary>
public class LlmToolDefinition
{
    public LlmToolDefinition() { }

    public LlmToolDefinition(string name, string description, string parametersSchemaJson)
    {
        Name = name;
        Description = description;
        ParametersSchemaJson = parametersSchemaJson;
    }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersSchemaJson { get; set; } = "{}";

    public JsonElement ParseParametersSchema()
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(ParametersSchemaJson) ? "{}" : ParametersSchemaJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}
