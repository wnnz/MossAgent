using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MossAgent.Domain;

namespace MossAgent.Testing;

internal sealed class AuthenticatedProxyTestServer : IAsyncDisposable
{
    public const string Username = "proxy-fixture-user";
    public const string Password = "proxy-fixture-secret";
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime;
    private readonly ProxyProtocol? _protocol;
    private readonly Func<string, string> _response;
    private readonly string _expectedHost;
    private readonly int _expectedPort;
    private readonly Task _serving;
    private int _rejections;
    private int _connections;

    public AuthenticatedProxyTestServer(ProxyProtocol? protocol, Func<string, string> response,
        CancellationToken cancellationToken, string expectedHost = "models.example.test", int expectedPort = 80)
    {
        _protocol = protocol;
        _response = response;
        _expectedHost = expectedHost;
        _expectedPort = expectedPort;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serving = ServeAsync(_lifetime.Token);
    }

    public int Port { get; }
    public int Rejections => Volatile.Read(ref _rejections);
    public int Connections => Volatile.Read(ref _connections);
    public ConcurrentQueue<(string Method, string Target, bool Authenticated,
        bool ProxyAuthorization, bool CredentialLeak)> Requests
    { get; } = new();

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _listener.Stop();
        try { await _serving.ConfigureAwait(false); }
        finally { _lifetime.Dispose(); }
    }

    private async Task ServeAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                Interlocked.Increment(ref _connections);
                try { await HandleAsync(client.GetStream(), token).ConfigureAwait(false); }
                catch (IOException) when (!token.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (SocketException) when (token.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
    }

    private async Task HandleAsync(Stream stream, CancellationToken token)
    {
        if (_protocol == ProxyProtocol.Socks5 && !await Socks5TestHandshake.AcceptAuthenticatedAsync(
                stream, _expectedHost, _expectedPort, Username, Password, token,
                () => Interlocked.Increment(ref _rejections)).ConfigureAwait(false))
        {
            return;
        }
        var request = await ProxyTestRequestReader.ReadAsync(stream, token).ConfigureAwait(false);
        var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(Username + ":" + Password));
        var authenticated = _protocol != ProxyProtocol.Http || request.Headers.GetValueOrDefault("Proxy-Authorization") == expected;
        var sensitive = request.Body + request.Headers.GetValueOrDefault("Authorization")
            + request.Headers.GetValueOrDefault("x-goog-api-key");
        Requests.Enqueue((request.Method, request.Target, authenticated,
            request.Headers.ContainsKey("Proxy-Authorization"),
            sensitive.Contains(Username, StringComparison.Ordinal) || sensitive.Contains(Password, StringComparison.Ordinal)
                || sensitive.Contains(expected, StringComparison.Ordinal)));
        if (!authenticated)
        {
            Interlocked.Increment(ref _rejections);
            await WriteAsync(stream, 407, "Rejected " + Username + " " + Password, token).ConfigureAwait(false);
            return;
        }
        if (!Uri.TryCreate(request.Target, UriKind.Absolute, out var target))
            target = new Uri(new Uri("http://models.example.test"), request.Target);
        if (target.Host != "models.example.test" && target.Host != "127.0.0.1")
            throw new InvalidDataException("Unexpected proxy fixture target.");
        await WriteAsync(stream, 200, _response(target.PathAndQuery), token).ConfigureAwait(false);
    }

    private static async Task WriteAsync(Stream stream, int status, string body, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var challenge = status == 407 ? "Proxy-Authenticate: Basic realm=\"local-fixture\"\r\n" : string.Empty;
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Fixture\r\n{challenge}"
            + $"Content-Type: text/event-stream\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }
}
