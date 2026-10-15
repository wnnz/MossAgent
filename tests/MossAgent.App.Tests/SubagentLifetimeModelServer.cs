using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class SubagentLifetimeModelServer : IAsyncDisposable
{
    public const string PartialReply = "正在检查代码，等待下一步。";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly Task _serving;
    private readonly bool _chunked;

    public SubagentLifetimeModelServer(CancellationToken token, bool chunked = false)
    {
        _chunked = chunked;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public IReadOnlyList<JsonElement> Requests => _requests.ToArray();
    public TaskCompletionSource ChildDisconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ChildFrameWritten { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Completion => _serving;

    private async Task ServeAsync(CancellationToken token)
    {
        for (var turn = 0; turn < 2; turn++)
        {
            using var client = await _listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            using var document = JsonDocument.Parse(await ReadBodyAsync(stream, token));
            var request = document.RootElement;
            _requests.Enqueue(request.Clone());
            var response = turn == 0 ? CreateDelegation(request)
                : JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = PartialReply });
            var bytes = Encoding.UTF8.GetBytes($"data: {response}\n\n" + (turn == 0 ? "data: [DONE]\n\n" : string.Empty));
            await WriteResponseAsync(stream, bytes, turn == 1, token);
            if (turn == 1)
            {
                ChildFrameWritten.TrySetResult();
                try
                {
                    var buffer = new byte[1];
                    if (await stream.ReadAsync(buffer, token) != 0)
                        throw new InvalidDataException("Unexpected bytes on the held fixture response.");
                }
                catch (IOException) when (!token.IsCancellationRequested) { }
                token.ThrowIfCancellationRequested();
                ChildDisconnected.TrySetResult();
            }
        }
    }

    private async Task WriteResponseAsync(NetworkStream stream, byte[] bytes, bool child, CancellationToken token)
    {
        var framing = child ? (_chunked ? "Transfer-Encoding: chunked\r\n" : string.Empty)
            : $"Content-Length: {bytes.Length}\r\n";
        var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
            + framing + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        if (child && _chunked) await stream.WriteAsync(Encoding.ASCII.GetBytes($"{bytes.Length:x}\r\n"), token);
        await stream.WriteAsync(bytes, token);
        if (child && _chunked) await stream.WriteAsync("\r\n"u8.ToArray(), token);
    }

    private static string CreateDelegation(JsonElement request)
    {
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(name => name?.StartsWith("subagent_run__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new
            {
                type = "function_call",
                call_id = "lifetime-child",
                name,
                arguments = JsonSerializer.Serialize(new { agent = "review", task = "检查代码" })
            }
        });
    }

    private static async Task<string> ReadBodyAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var buffer = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_000_000)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) throw new EndOfStreamException();
            received.Write(buffer, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                var header = Encoding.ASCII.GetString(bytes, 0, bodyStart);
                var length = header.Split("\r\n").Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_000_000) throw new InvalidDataException("Unbounded fixture request.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
                return Encoding.UTF8.GetString(bytes, bodyStart, bodyLength);
        }
        throw new InvalidDataException("Fixture request exceeded its bound.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try { await _serving.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        finally { _stopping.Dispose(); }
    }
}
