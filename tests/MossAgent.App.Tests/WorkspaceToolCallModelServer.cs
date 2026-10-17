using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceToolCallModelServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly string _toolPrefix;
    private readonly JsonElement _arguments;
    private readonly Task _serving;
    private readonly int _conversations;

    public WorkspaceToolCallModelServer(string toolName, JsonElement arguments, CancellationToken token,
        bool useNamespacePrefix = false, int conversations = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(conversations, 1);
        _conversations = conversations;
        _toolPrefix = toolName.Replace('.', '_') + (useNamespacePrefix ? "_" : "__");
        _arguments = arguments.Clone();
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public CancellationToken ShutdownToken => _stopping.Token;
    public IReadOnlyList<string> Requests => _requests.ToArray();
    public Task Completion => _serving;

    private async Task ServeAsync(CancellationToken token)
    {
        for (var turn = 0; turn < 2 * _conversations; turn++)
        {
            using var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            var body = await ReadBodyAsync(stream, token).ConfigureAwait(false);
            _requests.Enqueue(body);
            using var document = JsonDocument.Parse(body);
            var response = CreateResponse(document.RootElement, turn % 2);
            var bytes = Encoding.UTF8.GetBytes($"data: {response}\n\ndata: [DONE]\n\n");
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, token).ConfigureAwait(false);
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        }
    }

    private string CreateResponse(JsonElement request, int turn)
    {
        if (turn == 1) return JsonSerializer.Serialize(new
        {
            type = "response.output_text.delta",
            delta = "MCP 链路验收已结束。"
        });
        var name = request.GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString())
            .Single(tool => tool?.StartsWith(_toolPrefix, StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new
            {
                type = "function_call",
                call_id = "mcp-flow",
                name,
                arguments = _arguments.GetRawText()
            }
        });
    }

    private static async Task<string> ReadBodyAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var chunk = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_000_000)
        {
            var count = await stream.ReadAsync(chunk, token).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException();
            received.Write(chunk, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                var header = Encoding.ASCII.GetString(bytes, 0, bodyStart);
                var length = header.Split("\r\n").Single(line =>
                    line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_000_000) throw new InvalidDataException("Unbounded model request.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
                return Encoding.UTF8.GetString(bytes, bodyStart, bodyLength);
        }
        throw new InvalidDataException("Model request exceeded fixture limit.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try { await _serving.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        _stopping.Dispose();
    }
}
