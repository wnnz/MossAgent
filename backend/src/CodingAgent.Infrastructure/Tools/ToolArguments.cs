using System.Text.Json;

namespace CodingAgent.Infrastructure.Tools;

/// <summary>工具参数解析辅助。</summary>
public static class ToolArguments
{
    public static string GetString(string argumentsJson, string key, string defaultValue = "")
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? defaultValue;
            }
        }
        catch (JsonException) { }
        return defaultValue;
    }

    public static bool GetBool(string argumentsJson, string key, bool defaultValue = false)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }
        }
        catch (JsonException) { }
        return defaultValue;
    }

    public static int GetInt(string argumentsJson, string key, int defaultValue)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetInt32();
            }
        }
        catch (JsonException) { }
        return defaultValue;
    }

    public static string Truncate(string text, int maxLength = 32_000) =>
        text.Length <= maxLength ? text : text[..maxLength] + $"\n[输出已截断，原始长度 {text.Length}]";
}
