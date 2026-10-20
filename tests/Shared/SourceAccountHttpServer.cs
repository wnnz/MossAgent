using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MossAgent.Testing;

internal sealed class SourceAccountHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime;
    private readonly Task _run;
    public ConcurrentQueue<string[]> Requests { get; } = new();
    public Uri Address { get; }

    public SourceAccountHttpServer(Func<int, string> respond, int requestCount, CancellationToken cancellationToken)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime.CancelAfter(TimeSpan.FromSeconds(20));
        _listener.Start();
        Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _run = RunAsync(respond, requestCount);
    }

    public Task Completion => _run;

    private async Task RunAsync(Func<int, string> respond, int count)
    {
        for (var index = 0; index < count; index++)
        {
            using var socket = await _listener.AcceptTcpClientAsync(_lifetime.Token);
            await using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var headers = new List<string>();
            var size = 0;
            while (await reader.ReadLineAsync(_lifetime.Token) is { Length: > 0 } line)
            {
                size += line.Length;
                if (size > 16384 || headers.Count > 64) throw new InvalidOperationException("Fixture header limit.");
                headers.Add(line);
            }
            Requests.Enqueue(headers.ToArray());
            await stream.WriteAsync(Encoding.UTF8.GetBytes(respond(index)), _lifetime.Token);
        }
    }

    public static string JsonResponse(string body, string status = "200 OK", string headers = "") =>
        $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n{headers}\r\n{body}";

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _listener.Stop();
        try { await _run; }
        catch (OperationCanceledException) { }
        catch (SocketException) when (_lifetime.IsCancellationRequested) { }
        finally { _lifetime.Dispose(); }
    }
}
