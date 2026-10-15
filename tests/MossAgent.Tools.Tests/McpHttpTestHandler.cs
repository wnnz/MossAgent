using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Mcp;

namespace MossAgent.Tools.Tests;

internal sealed class McpHttpTestHandler : HttpMessageHandler
{
    public ConcurrentQueue<JsonElement> Requests { get; } = new();
    public Func<JsonElement, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }
    public bool DeleteObserved { get; private set; }
    public bool SessionHeaderObserved { get; private set; }
    public bool IsDisposed { get; private set; }

    public StreamableHttpMcpClient CreateClient() => new(
        new McpServerProfile(Guid.NewGuid(), "fixture", McpTransportKind.StreamableHttp,
            null, "[]", null, "{}", new Uri("http://127.0.0.1/mcp/"), "{}", null, true),
        new HttpClient(this) { Timeout = Timeout.InfiniteTimeSpan });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SessionHeaderObserved |= request.Headers.TryGetValues("Mcp-Session-Id", out var values)
            && values.Contains("fixture-session");
        if (request.Method == HttpMethod.Delete)
        {
            DeleteObserved = true;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var message = document.RootElement.Clone();
        Requests.Enqueue(message);
        return Respond is null ? DefaultResponse(message) : await Respond(message, cancellationToken);
    }

    public static HttpResponseMessage DefaultResponse(JsonElement message)
    {
        var method = message.GetProperty("method").GetString();
        if (method?.StartsWith("notifications/", StringComparison.Ordinal) == true)
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        var result = method switch
        {
            "initialize" => (object)new { protocolVersion = "2025-03-26", capabilities = new { } },
            "tools/list" => new { tools = Array.Empty<object>() },
            _ => new { content = new[] { new { type = "text", text = "中文 🙂" } }, isError = false }
        };
        var response = JsonResponse(new { jsonrpc = "2.0", id = message.GetProperty("id").GetInt64(), result });
        if (method == "initialize") response.Headers.Add("Mcp-Session-Id", "fixture-session");
        return response;
    }

    public static HttpResponseMessage JsonResponse(object payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    };

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
