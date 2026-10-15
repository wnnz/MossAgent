using System.Text.Json;

namespace MossAgent.Mcp;

internal static class McpHttpResponseParser
{
    public static JsonElement? ReadMessage(string content, long expectedId)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("jsonrpc", out var version)
                || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0")
                throw new IOException("MCP HTTP 响应不是有效的 JSON-RPC 消息。");
            if (value.TryGetProperty("method", out _)) return null;
            if (!value.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number
                || !id.TryGetInt64(out var actualId))
                throw new IOException("MCP HTTP 响应缺少有效请求标识。");
            if (actualId != expectedId) return null;
            var hasResult = value.TryGetProperty("result", out var result);
            var hasError = value.TryGetProperty("error", out _);
            if (hasResult == hasError) throw new IOException("MCP HTTP 响应缺少有效结果。");
            if (hasError) throw new IOException("MCP server 返回协议错误。");
            if (result.ValueKind != JsonValueKind.Object) throw new IOException("MCP HTTP 响应结果格式无效。");
            return value.Clone();
        }
        catch (JsonException)
        {
            throw new IOException("MCP HTTP 响应不是有效的 JSON-RPC 消息。");
        }
    }
}
