using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class ModelSelectionResponseServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly Task _serving;

    public ModelSelectionResponseServer(CancellationToken token, bool pauseResponse = false)
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        if (!pauseResponse) _release.SetResult();
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public IReadOnlyList<JsonElement> Requests => _requests.ToArray();
    public void ReleaseResponses() => _release.TrySetResult();

    private async Task ServeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            using var document = JsonDocument.Parse(await ReadBodyAsync(stream, token));
            _requests.Enqueue(document.RootElement.Clone());
            await _release.Task.WaitAsync(token);
            var data = JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = "模型选择链路已完成。" });
            var bytes = Encoding.UTF8.GetBytes($"data: {data}\n\ndata: [DONE]\n\n");
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(bytes, token);
        }
    }

    private static async Task<string> ReadBodyAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var chunk = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_000_000)
        {
            var count = await stream.ReadAsync(chunk, token);
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
        await _stopping.CancelAsync();
        _listener.Stop();
        try { await _serving; }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        _stopping.Dispose();
    }
}
