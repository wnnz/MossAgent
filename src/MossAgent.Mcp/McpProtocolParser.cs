using System.Text.Json;

namespace MossAgent.Mcp;

internal static class McpProtocolParser
{
    public static IReadOnlyList<McpToolDefinition> ReadTools(JsonElement result)
    {
        if (!result.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var definitions = new List<McpToolDefinition>();
        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString() ?? string.Empty;
            var description = tool.TryGetProperty("description", out var descriptionValue)
                ? descriptionValue.GetString() ?? string.Empty
                : string.Empty;
            var schema = tool.TryGetProperty("inputSchema", out var schemaValue)
                ? schemaValue.Clone()
                : JsonDocument.Parse("{\"type\":\"object\"}").RootElement.Clone();
            var readOnly = tool.TryGetProperty("annotations", out var annotations)
                && annotations.TryGetProperty("readOnlyHint", out var hint)
                && hint.ValueKind == JsonValueKind.True;
            definitions.Add(new McpToolDefinition(name, description, schema, readOnly));
        }

        return definitions;
    }

    public static McpCallResult ReadCallResult(JsonElement result)
    {
        var isError = result.TryGetProperty("isError", out var error)
            && error.ValueKind == JsonValueKind.True;
        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return new McpCallResult(isError, result.GetRawText());
        }

        var parts = content.EnumerateArray().Select(ReadContentPart);
        return new McpCallResult(isError, string.Join(Environment.NewLine, parts));
    }

    private static string ReadContentPart(JsonElement part)
    {
        return part.TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : part.GetRawText();
    }
}

