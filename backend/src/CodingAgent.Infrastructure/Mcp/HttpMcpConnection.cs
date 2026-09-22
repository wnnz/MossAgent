using System.Text;
using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;

namespace CodingAgent.Infrastructure.Mcp;

/// <summary>streamable HTTP 传输 MCP 连接：POST JSON-RPC，支持 JSON 与 SSE 两种响应。</summary>
public class HttpMcpConnection(McpServer server) : IMcpConnection
{
    public int ServerId => server.Id;
    public string ServerName => server.Name;
    public bool IsConnected { get; private set; }

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private int _nextId;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsConnected || string.IsNullOrWhiteSpace(server.Url))
        {
            return;
        }
        await RequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "CodingAgent", version = "1.0.0" },
        }, ct);
        await NotifyAsync("notifications/initialized", ct);
        IsConnected = true;
    }

    public async Task<List<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var result = await RequestAsync("tools/list", new { }, ct);
        var tools = new List<McpToolInfo>();
        if (result.TryGetProperty("tools", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in arr.EnumerateArray())
            {
                tools.Add(new McpToolInfo
                {
                    Name = t.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    Description = t.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                    SchemaJson = t.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : "{}",
                });
            }
        }
        return tools;
    }

    public async Task<string> CallToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        var args = string.IsNullOrWhiteSpace(argumentsJson) ? (object)new { } : JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var result = await RequestAsync("tools/call", new { name = toolName, arguments = args }, ct);

        if (result.TryGetProperty("isError", out var isError) && isError.GetBoolean())
        {
            return $"工具执行错误: {ExtractText(result)}";
        }
        return ExtractText(result);
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
        var responseText = await PostAsync(payload, expectNotification: false, ct);

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : "MCP 错误";
            throw new InvalidOperationException($"MCP 错误: {message}");
        }
        return root.TryGetProperty("result", out var result) ? result.Clone() : JsonSerializer.Deserialize<JsonElement>("{}");
    }

    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
        await PostAsync(payload, expectNotification: true, ct);
    }

    private async Task<string> PostAsync(string payload, bool expectNotification, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, server.Url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        if (!expectNotification)
        {
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        }
        if (!string.IsNullOrWhiteSpace(server.HeadersJson))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(server.HeadersJson);
            foreach (var (k, v) in headers ?? [])
            {
                request.Headers.TryAddWithoutValidation(k, v);
            }
        }

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
        var body = await response.Content.ReadAsStringAsync(ct);

        if (contentType.Contains("event-stream"))
        {
            // SSE 响应：取最后一个 data: 行的 JSON
            var lastData = body.Split('\n')
                .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                .Select(l => l["data:".Length..].Trim())
                .LastOrDefault(l => l.Length > 0);
            return lastData ?? "{}";
        }
        return body.Length == 0 ? "{}" : body;
    }

    private static string ExtractText(JsonElement result)
    {
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var c in content.EnumerateArray())
            {
                if (c.TryGetProperty("type", out var type) && type.GetString() == "text" && c.TryGetProperty("text", out var text))
                {
                    sb.AppendLine(text.GetString());
                }
            }
            return sb.ToString().TrimEnd();
        }
        return result.GetRawText();
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
