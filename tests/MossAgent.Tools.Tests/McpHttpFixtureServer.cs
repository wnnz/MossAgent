using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.Tools.Tests;

internal sealed class McpHttpFixtureServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;

    public McpHttpFixtureServer()
    {
        var port = GetFreePort();
        Endpoint = new Uri($"http://127.0.0.1:{port}/mcp/");
        _listener.Prefixes.Add(Endpoint.ToString());
        _listener.Start();
        _loop = RunAsync();
    }

    public Uri Endpoint { get; }
    public bool SessionHeaderObserved { get; private set; }
    public bool CustomHeaderObserved { get; private set; }
    public bool DeleteObserved { get; private set; }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (HttpListenerException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }

        _listener.Close();
        _cancellation.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().WaitAsync(_cancellation.Token);
            await HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        CustomHeaderObserved |= context.Request.Headers["X-Moss-Fixture"] == "enabled";
        SessionHeaderObserved |= context.Request.Headers["Mcp-Session-Id"] == "fixture-session";
        if (context.Request.HttpMethod == HttpMethod.Delete.Method)
        {
            DeleteObserved = true;
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            context.Response.Close();
            return;
        }

        using var document = await JsonDocument.ParseAsync(context.Request.InputStream);
        var root = document.RootElement;
        var method = root.GetProperty("method").GetString();
        if (method == "notifications/initialized")
        {
            context.Response.StatusCode = (int)HttpStatusCode.Accepted;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
            return;
        }

        var id = root.GetProperty("id").GetInt64();
        object result = method switch
        {
            "initialize" => InitializeResult(),
            "tools/list" => ToolsResult(),
            "tools/call" => CallResult(root.GetProperty("params")),
            _ => new { }
        };
        if (method == "initialize")
        {
            context.Response.Headers["Mcp-Session-Id"] = "fixture-session";
        }

        var responsePayload = new { jsonrpc = "2.0", id, result };
        if (method == "tools/list")
        {
            await WriteSseAsync(context.Response, responsePayload);
            return;
        }

        await WriteJsonAsync(context.Response, responsePayload);
    }

    private static object InitializeResult() => new
    {
        protocolVersion = "2025-03-26",
        capabilities = new { tools = new { } },
        serverInfo = new { name = "fixture", version = "1.0" }
    };

    private static object ToolsResult() => new
    {
        tools = new[]
        {
            new
            {
                name = "echo",
                description = "Echo text",
                inputSchema = new
                {
                    type = "object",
                    required = new[] { "text" },
                    properties = new { text = new { type = "string" } }
                },
                annotations = new { readOnlyHint = true }
            }
        }
    };

    private static object CallResult(JsonElement parameters)
    {
        var text = parameters.GetProperty("arguments").GetProperty("text").GetString();
        return new
        {
            content = new[] { new { type = "text", text = $"echo:{text}" } },
            isError = false
        };
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.LongLength;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static async Task WriteSseAsync(HttpListenerResponse response, object payload)
    {
        var notification = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "notifications/progress",
            @params = new { progress = 0.5 }
        });
        var result = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes($"data: {notification}\n\ndata: {result}\n\n");
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/event-stream";
        response.ContentLength64 = bytes.LongLength;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
