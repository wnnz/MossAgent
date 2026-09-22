using System.Text.Json;

namespace MossAgent.Mcp;

internal static class McpHttpResponseParser
{
    public static JsonElement Parse(string content, string? mediaType, long? expectedId)
    {
        if (!string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return ParseJson(content);
        }

        foreach (var payload in ReadEventData(content))
        {
            var value = ParseJson(payload);
            if (expectedId is null || HasId(value, expectedId.Value))
            {
                return value;
            }
        }

        throw new InvalidDataException("MCP SSE 响应中没有匹配的 JSON-RPC 结果。");
    }

    private static IEnumerable<string> ReadEventData(string content)
    {
        var data = new List<string>();
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Count > 0)
                {
                    yield return string.Join('\n', data);
                    data.Clear();
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Add(line[5..].TrimStart());
            }
        }

        if (data.Count > 0)
        {
            yield return string.Join('\n', data);
        }
    }

    private static JsonElement ParseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static bool HasId(JsonElement value, long expectedId) =>
        value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty("id", out var id)
        && id.ValueKind == JsonValueKind.Number
        && id.TryGetInt64(out var actualId)
        && actualId == expectedId;
}
