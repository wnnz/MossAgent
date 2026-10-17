using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ProviderUiWorkflowServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly ProviderProtocol _protocol;
    private readonly ConcurrentQueue<string> _paths = new();
    private readonly ConcurrentQueue<JsonElement> _requests = new();
    private readonly Task _serving;
    private int _discoveries;

    public ProviderUiWorkflowServer(ProviderProtocol protocol, CancellationToken token)
    {
        _protocol = protocol;
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        ModelId = protocol == ProviderProtocol.GeminiGenerateContent ? "gemini-3.8-flash" : "gpt-5";
        _serving = ServeAsync(_stopping.Token);
    }

    public Uri BaseUri { get; }
    public string ModelId { get; }
    public int Discoveries => Volatile.Read(ref _discoveries);
    public IReadOnlyList<string> Paths => _paths.ToArray();
    public IReadOnlyList<JsonElement> Requests => _requests.ToArray();

    private async Task ServeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(token);
            await using var stream = client.GetStream();
            var request = await ProxyTestRequestReader.ReadAsync(stream, token);
            var path = new Uri(BaseUri, request.Target).AbsolutePath;
            _paths.Enqueue(path);
            if (request.Method == "GET" && path == "/v1/models")
            {
                var failed = Interlocked.Increment(ref _discoveries) == 1;
                var body = failed ? """{"error":{"message":"synthetic-private-provider-key"}}"""
                    : ProviderRoutingTestProtocol.Models(_protocol).Replace("fixture-model", ModelId, StringComparison.Ordinal);
                await RespondAsync(stream, failed ? "503 Service Unavailable" : "200 OK", "application/json", body, token);
            }
            else if (request.Method == "POST")
            {
                using var document = JsonDocument.Parse(request.Body);
                _requests.Enqueue(document.RootElement.Clone());
                await RespondAsync(stream, "200 OK", "text/event-stream", ProviderRoutingTestProtocol.Stream(_protocol), token);
            }
            else
            {
                throw new InvalidDataException("Unexpected provider workflow request.");
            }
        }
    }

    private static async Task RespondAsync(Stream stream, string status, string contentType, string body, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\n"
            + $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
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
