using System.Text;
using System.Text.Json;

namespace MossAgent.Mcp;

internal static class McpHttpResponseReader
{
    public const int MaximumResponseBytes = 2 * 1024 * 1024;

    public static async Task<JsonElement> ReadAsync(HttpContent content, long expectedId,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
            throw new IOException("MCP HTTP 响应超过 2 MiB 大小限制。");
        var mediaType = content.Headers.ContentType?.MediaType;
        var isSse = string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase);
        if (!isSse && !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new IOException("MCP HTTP 响应必须使用 JSON 或 SSE 格式。");
        using var stream = new McpLimitedReadStream(
            await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), MaximumResponseBytes);
        using var reader = new StreamReader(stream, new UTF8Encoding(true, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        try
        {
            if (isSse)
                return await ReadSseAsync(reader, expectedId, cancellationToken).ConfigureAwait(false);
            var payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return McpHttpResponseParser.ReadMessage(payload, expectedId)
                ?? throw new IOException("MCP HTTP 响应中没有匹配的 JSON-RPC 结果。");
        }
        catch (DecoderFallbackException)
        {
            throw new IOException("MCP HTTP 响应不是有效的 UTF-8 内容。");
        }
    }

    private static async Task<JsonElement> ReadSseAsync(StreamReader reader, long expectedId,
        CancellationToken cancellationToken)
    {
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length == 0) continue;
                var message = McpHttpResponseParser.ReadMessage(data.ToString(), expectedId);
                data.Clear();
                if (message is { } response) return response;
            }
            else if (line == "data" || line.StartsWith("data:", StringComparison.Ordinal))
            {
                var payload = line.Length > 5 ? line[5..] : string.Empty;
                if (payload.StartsWith(' ')) payload = payload[1..];
                if (data.Length > 0) data.Append('\n');
                data.Append(payload);
            }
        }
        if (data.Length > 0 && McpHttpResponseParser.ReadMessage(data.ToString(), expectedId) is { } result)
            return result;
        throw new IOException("MCP SSE 响应中没有匹配的 JSON-RPC 结果。");
    }
}
