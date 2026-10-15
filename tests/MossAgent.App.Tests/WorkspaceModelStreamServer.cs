using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceModelStreamServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ConcurrentQueue<(string Target, JsonElement Body)> _requests = new();
    private readonly Task _serving;

    public WorkspaceModelStreamServer(Func<JsonElement, int, string> response, CancellationToken token,
        string basePath = "v1/")
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/{basePath}");
        _serving = ServeAsync(response, _stopping.Token);
    }

    public Uri BaseUri { get; }
    public IReadOnlyList<(string Target, JsonElement Body)> Requests => _requests.ToArray();

    private async Task ServeAsync(Func<JsonElement, int, string> response, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, token).ConfigureAwait(false);
            var turn = _requests.Count;
            _requests.Enqueue(request);
            var bytes = Encoding.UTF8.GetBytes(response(request.Body, turn));
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
                + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, token).ConfigureAwait(false);
            await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        }
    }

    private static async Task<(string Target, JsonElement Body)> ReadRequestAsync(NetworkStream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var chunk = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        var target = string.Empty;
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
                var lines = header.Split("\r\n");
                target = lines[0];
                var length = lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_000_000) throw new InvalidDataException("Unbounded model request.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
            {
                using var document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes, bodyStart, bodyLength));
                return (target, document.RootElement.Clone());
            }
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
