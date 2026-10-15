using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceMcpHttpServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentBag<Task> _handlers = [];
    private readonly Task _loop;
    private readonly JsonElement _schema;

    public WorkspaceMcpHttpServer(JsonElement? schema = null)
    {
        _schema = schema?.Clone() ?? JsonSerializer.SerializeToElement(new
        {
            type = "object",
            required = new[] { "message" },
            properties = new { message = new { type = "string" } }
        });
        var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        var number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        Endpoint = new Uri($"http://127.0.0.1:{number}/mcp/");
        _listener.Prefixes.Add(Endpoint.AbsoluteUri);
        _listener.Start();
        _loop = RunAsync();
    }

    public Uri Endpoint { get; }
    public CancellationToken ShutdownToken => _shutdown.Token;
    public TaskCompletionSource<long> CallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<long> CancellationReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool CustomHeaderObserved { get; private set; }
    public bool SessionHeaderObserved { get; private set; }
    public bool DeleteObserved { get; private set; }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        await _loop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await Task.WhenAll(_handlers).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _listener.Close();
        _shutdown.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().WaitAsync(_shutdown.Token).ConfigureAwait(false);
            _handlers.Add(HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            CustomHeaderObserved |= context.Request.Headers["X-Moss-Fixture"] == "workflow-header";
            SessionHeaderObserved |= context.Request.Headers["Mcp-Session-Id"] == "workflow-session";
            if (context.Request.HttpMethod == "DELETE")
            {
                DeleteObserved = true;
                context.Response.StatusCode = 204;
                return;
            }
            using var document = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: _shutdown.Token).ConfigureAwait(false);
            var request = document.RootElement;
            var method = request.GetProperty("method").GetString();
            if (method?.StartsWith("notifications/", StringComparison.Ordinal) == true)
            {
                if (method == "notifications/cancelled")
                    CancellationReceived.TrySetResult(request.GetProperty("params").GetProperty("requestId").GetInt64());
                context.Response.StatusCode = 202;
                context.Response.ContentLength64 = 0;
                return;
            }
            var id = request.GetProperty("id").GetInt64();
            if (method == "tools/call") await CallAsync(context.Response, request, id).ConfigureAwait(false);
            else await WriteDiscoveryAsync(context.Response, method, id).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (HttpListenerException) { }
        catch (IOException) { }
        finally { context.Response.Close(); }
    }

    private Task WriteDiscoveryAsync(HttpListenerResponse response, string? method, long id)
    {
        if (method == "initialize") response.Headers["Mcp-Session-Id"] = "workflow-session";
        object result = method == "initialize"
            ? new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { tools = new { } },
                serverInfo = new { name = "workflow", version = "1.0" }
            }
            : new
            {
                tools = new[]
                {
                    new
                    {
                        name = "echo", description = "Local workflow fixture",
                        inputSchema = _schema
                    }
                }
            };
        return WriteJsonAsync(response, new { jsonrpc = "2.0", id, result });
    }

    private async Task CallAsync(HttpListenerResponse response, JsonElement request, long id)
    {
        var message = request.GetProperty("params").GetProperty("arguments").GetProperty("message").GetString();
        CallStarted.TrySetResult(id);
        if (message == "error")
        {
            await WriteJsonAsync(response, new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32000, message = "fixture-secret-marker" }
            }).ConfigureAwait(false);
            return;
        }
        var text = message switch { "verbose" => new string('x', 200_000), "tool-error" => "fixture-secret-marker", _ => message };
        var result = new { content = new[] { new { type = "text", text } }, isError = message == "tool-error" };
        var payload = message == "wait" ? ": waiting\n\n"
            : "data: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}\n\n"
                + $"data: {JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result })}\n\n";
        response.StatusCode = 200;
        response.ContentType = "text/event-stream";
        response.SendChunked = true;
        await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(payload), _shutdown.Token).ConfigureAwait(false);
        await response.OutputStream.FlushAsync(_shutdown.Token).ConfigureAwait(false);
        if (message == "wait") await CancellationReceived.Task.WaitAsync(_shutdown.Token).ConfigureAwait(false);
        else await Task.Delay(Timeout.InfiniteTimeSpan, _shutdown.Token).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }
}
