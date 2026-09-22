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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await SendRequestAsync("initialize", new
        {
            protocolVersion = "2025-03-26",
            capabilities = new { },
            clientInfo = new { name = "MossAgent", version = "0.1.0" }
        }, cancellationToken);
        await SendNotificationAsync("notifications/initialized", new { }, cancellationToken);
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync("tools/list", new { }, cancellationToken);
        return McpProtocolParser.ReadTools(result);
    }

    public async Task<McpCallResult> CallToolAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync(
            "tools/call", new { name, arguments }, cancellationToken);
        return McpProtocolParser.ReadCallResult(result);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await TerminateSessionAsync();
        }
        finally
        {
            client.Dispose();
        }
    }

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var response = await SendAsync(
            new { jsonrpc = "2.0", id, method, @params = parameters },
            cancellationToken,
            expectedId: id);
        if (response.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException(error.GetRawText());
        }

        return response.GetProperty("result").Clone();
    }

    private async Task SendNotificationAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken)
    {
        await SendAsync(
            new { jsonrpc = "2.0", method, @params = parameters }, cancellationToken,
            allowEmpty: true);
    }

    private async Task<JsonElement> SendAsync(
        object payload,
        CancellationToken cancellationToken,
        bool allowEmpty = false,
        long? expectedId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            profile.Url ?? throw new InvalidOperationException("HTTP MCP server 缺少 URL。"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-03-26");
        if (_sessionId is not null)
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var values))
        {
            _sessionId = values.FirstOrDefault();
        }

        if (allowEmpty && response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return McpHttpResponseParser.Parse(
            content, response.Content.Headers.ContentType?.MediaType, expectedId);
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
            using var response = await client.SendAsync(request, CancellationToken.None);
        }
        catch
        {
            // Session termination is best-effort during disposal.
        }
    }
}
