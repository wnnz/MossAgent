using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MossAgent.Providers.Tests;

internal sealed class ImageInputLoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly Task _serving;

    public ImageInputLoopbackServer(string responseBody, CancellationToken token)
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _stopping.CancelAfter(TimeSpan.FromSeconds(20));
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(responseBody, _stopping.Token);
    }

    public Uri BaseUri { get; }
    public string? RequestLine { get; private set; }
    public string? RequestBody { get; private set; }
    public Task Completion => _serving;

    private async Task ServeAsync(string responseBody, CancellationToken token)
    {
        using var client = await _listener.AcceptTcpClientAsync(token);
        await using var stream = client.GetStream();
        var headers = await ReadHeadersAsync(stream, token);
        var lines = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        RequestLine = lines[0];
        var length = lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        var byteCount = int.Parse(length.Split(':', 2)[1].Trim());
        if (byteCount is < 1 or > 1_048_576)
            throw new InvalidDataException("Image loopback request exceeds its fixture limit.");
        var bytes = new byte[byteCount];
        await stream.ReadExactlyAsync(bytes, token);
        RequestBody = new UTF8Encoding(false, true).GetString(bytes);
        var response = Encoding.UTF8.GetBytes(responseBody);
        var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
            + $"Content-Length: {response.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(response, token);
        await stream.FlushAsync(token);
    }

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var next = new byte[1];
        while (received.Length < 16_384)
        {
            if (await stream.ReadAsync(next, token) == 0) throw new EndOfStreamException();
            received.WriteByte(next[0]);
            if (received.Length >= 4 && received.GetBuffer().AsSpan((int)received.Length - 4, 4).SequenceEqual("\r\n\r\n"u8))
                return Encoding.ASCII.GetString(received.GetBuffer(), 0, (int)received.Length);
        }
        throw new InvalidDataException("Image loopback headers exceed their fixture limit.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        try { await _serving; }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        finally { _stopping.Dispose(); }
    }
}
