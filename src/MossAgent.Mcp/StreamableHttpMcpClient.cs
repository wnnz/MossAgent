using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.Mcp;

public sealed class StreamableHttpMcpClient(
    McpServerProfile profile,
    HttpClient client) : IMcpClient
{
    private long _nextId;
    private string? _sessionId;
    private readonly CancellationTokenSource _shutdown = new();
    private int _disposed;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendRequestAsync("initialize", new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "MossAgent", version = "0.1.0" }
            }, cancellationToken).ConfigureAwait(false);
            await SendNotificationAsync("notifications/initialized", new { }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync("tools/list", new { }, cancellationToken).ConfigureAwait(false);
        return McpProtocolParser.ReadTools(result);
    }

    public async Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync(
            "tools/call", new { name, arguments }, cancellationToken).ConfigureAwait(false);
        return McpProtocolParser.ReadCallResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
            await TerminateSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            client.Dispose();
            _shutdown.Dispose();
        }
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        var id = Interlocked.Increment(ref _nextId);
        try
        {
            var response = await SendAsync(new { jsonrpc = "2.0", id, method, @params = parameters },
                cancellationToken, expectedId: id, acceptSessionId: method == "initialize").ConfigureAwait(false);
            return response.GetProperty("result").Clone();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested
            && method != "initialize" && _disposed == 0)
        {
            await NotifyCancellationAsync(id).ConfigureAwait(false);
            throw;
        }
    }

    private async Task SendNotificationAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        await SendAsync(
            new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken,
            expectedId: null).ConfigureAwait(false);
    }

    private async Task NotifyCancellationAsync(long id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await SendNotificationAsync("notifications/cancelled", new { requestId = id }, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    private async Task<JsonElement> SendAsync(
        object payload,
        CancellationToken cancellationToken,
        long? expectedId,
        bool acceptSessionId = false)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        using var request = CreateRequest(payload);
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"MCP HTTP 服务请求失败（{(int)response.StatusCode}）。", null, response.StatusCode);
        if (acceptSessionId && response.Headers.TryGetValues("Mcp-Session-Id", out var values))
            _sessionId = values.FirstOrDefault();
        return expectedId is { } id
            ? await McpHttpResponseReader.ReadAsync(response.Content, id, linked.Token).ConfigureAwait(false)
            : default;
    }

    private HttpRequestMessage CreateRequest(object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (bytes.Length > McpHttpResponseReader.MaximumResponseBytes)
            throw new IOException("MCP HTTP 请求超过 2 MiB 大小限制。");
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            profile.Url ?? throw new InvalidOperationException("HTTP MCP server 缺少 URL。"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-03-26");
        if (_sessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }

        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };
        return request;
    }

    private async Task TerminateSessionAsync()
    {
        if (_sessionId is null || profile.Url is null)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, profile.Url);
        request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-03-26");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
