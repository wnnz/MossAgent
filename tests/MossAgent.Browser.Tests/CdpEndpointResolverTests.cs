using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MossAgent.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class CdpEndpointResolverTests
{
    [Fact]
    public async Task DirectLoopbackWebSocket_DoesNotRequireDiscovery()
    {
        var endpoint = new Uri("ws://127.0.0.1:9222/devtools/browser/test");

        var resolved = await CdpEndpointResolver.ResolveAsync(
            endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(endpoint, resolved);
    }

    [Fact]
    public async Task HttpDiscovery_AcceptsLoopbackWebSocketOnConfiguredPort()
    {
        var resolved = await ResolveFromResponseAsync(port => JsonResponse(
            $"ws://127.0.0.1:{port}/devtools/browser/test"));

        Assert.Equal("127.0.0.1", resolved.Host);
        Assert.EndsWith("/devtools/browser/test", resolved.AbsolutePath);
    }

    [Theory]
    [InlineData("ws://example.com:{port}/devtools/browser/test")]
    [InlineData("ws://127.0.0.1:{otherPort}/devtools/browser/test")]
    [InlineData("ws://user:secret@127.0.0.1:{port}/devtools/browser/test")]
    public async Task HttpDiscovery_RejectsUnapprovedWebSocket(string websocketTemplate)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveFromResponseAsync(port => JsonResponse(websocketTemplate
                .Replace("{port}", port.ToString(), StringComparison.Ordinal)
                .Replace("{otherPort}", (port + 1).ToString(), StringComparison.Ordinal))));

        Assert.Contains("不安全", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task HttpDiscovery_DoesNotFollowRedirects()
    {
        const string redirect = "HTTP/1.1 302 Found\r\n"
            + "Location: https://example.com/json/version\r\n"
            + "Content-Length: 0\r\nConnection: close\r\n\r\n";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveFromResponseAsync(_ => redirect));

        Assert.Contains("安全", error.Message);
    }

    [Fact]
    public async Task HttpDiscovery_RejectsOversizedResponseBeforeReadingBody()
    {
        const string response = "HTTP/1.1 200 OK\r\n"
            + "Content-Type: application/json\r\n"
            + "Content-Length: 1048577\r\nConnection: close\r\n\r\n";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ResolveFromResponseAsync(_ => response));

        Assert.Contains("安全", error.Message);
    }

    private static string JsonResponse(string websocket)
    {
        var body = JsonSerializer.Serialize(new { webSocketDebuggerUrl = websocket });
        var length = Encoding.UTF8.GetByteCount(body);
        return $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
            + $"Content-Length: {length}\r\nConnection: close\r\n\r\n{body}";
    }

    private static async Task<Uri> ResolveFromResponseAsync(Func<int, string> responseFactory)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var server = ServeOneAsync(listener, responseFactory(port), timeout.Token);

        try
        {
            return await CdpEndpointResolver.ResolveAsync(
                new Uri($"http://127.0.0.1:{port}/"), timeout.Token);
        }
        finally
        {
            await server;
        }
    }

    private static async Task ServeOneAsync(
        TcpListener listener, string response, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var request = new MemoryStream();
        var buffer = new byte[1024];
        while (request.Length < 8192)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            request.Write(buffer, 0, count);
            if (request.GetBuffer().AsSpan(0, (int)request.Length)
                .IndexOf("\r\n\r\n"u8) >= 0) break;
        }

        Assert.StartsWith("GET /json/version HTTP/1.1",
            Encoding.ASCII.GetString(request.ToArray()));
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response), cancellationToken);
    }
}
