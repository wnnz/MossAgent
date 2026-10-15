using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Browser;

internal static class CdpEndpointResolver
{
    private const int MaximumDiscoveryBytes = 1_048_576;

    public static async Task<Uri> ResolveAsync(
        Uri endpoint, CancellationToken cancellationToken)
    {
        if (!BrowserCdpEndpointPolicy.IsAllowed(endpoint))
        {
            throw new InvalidOperationException("CDP 地址必须是不含凭据的本机地址。");
        }

        if (endpoint.Scheme is "ws" or "wss")
        {
            return endpoint;
        }

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri(endpoint, "/json/version"));
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentLength is > MaximumDiscoveryBytes)
        {
            throw new InvalidOperationException("无法从本机 CDP 地址安全地获取浏览器连接信息。");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return ParseWebSocketEndpoint(await ReadBoundedAsync(stream, cancellationToken), endpoint);
    }

    private static Uri ParseWebSocketEndpoint(byte[] body, Uri endpoint)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("CDP 浏览器连接信息不是有效 JSON。");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("webSocketDebuggerUrl", out var value)
                || value.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var websocket)
                || websocket.Scheme is not ("ws" or "wss")
                || !BrowserCdpEndpointPolicy.IsAllowed(websocket)
                || websocket.Port != endpoint.Port)
            {
                throw new InvalidOperationException("本机 CDP 地址返回了不安全的 WebSocket 连接地址。");
            }

            return websocket;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) return output.ToArray();
            if (output.Length + count > MaximumDiscoveryBytes)
            {
                throw new InvalidOperationException("CDP 浏览器连接信息超过大小限制。");
            }

            output.Write(buffer, 0, count);
        }
    }
}
