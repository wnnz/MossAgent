using System.Net;
using System.Net.Sockets;
using System.Text;
using MossAgent.Domain;
using MossAgent.Infrastructure.Networking;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProxyConnectionTesterTests
{
    [Fact]
    public async Task TestAsync_UsesHttpProxyForProbe()
    {
        using var listener = StartListener(out var port);
        var cancellationToken = TestContext.Current.CancellationToken;
        var server = ServeHttpProxyAsync(listener, cancellationToken);
        var tester = new ProxyConnectionTester(new Uri("http://probe.test/status"));

        var result = await tester.TestAsync(
            CreateProxy(ProxyProtocol.Http, port), cancellationToken);

        Assert.True(result.IsSuccess, result.Message);
        Assert.StartsWith("GET http://probe.test/status", await server);
    }

    [Fact]
    public async Task TestAsync_UsesSocks5ProxyForProbe()
    {
        using var listener = StartListener(out var port);
        var cancellationToken = TestContext.Current.CancellationToken;
        var server = ServeSocks5ProxyAsync(listener, cancellationToken);
        var tester = new ProxyConnectionTester(new Uri("http://probe.test/status"));

        var result = await tester.TestAsync(
            CreateProxy(ProxyProtocol.Socks5, port), cancellationToken);

        Assert.True(result.IsSuccess, result.Message);
        Assert.StartsWith("GET /status", await server);
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static ProxyProfile CreateProxy(ProxyProtocol protocol, int port) =>
        new(
            Guid.NewGuid(), "test", protocol, "127.0.0.1", port,
            null, null, false, true);

    private static async Task<string> ServeHttpProxyAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var request = await ReadHeadersAsync(stream, cancellationToken);
        await WriteNoContentAsync(stream, cancellationToken);
        return request;
    }

    private static async Task<string> ServeSocks5ProxyAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var greeting = await ReadExactAsync(stream, 2, cancellationToken);
        await ReadExactAsync(stream, greeting[1], cancellationToken);
        await stream.WriteAsync(new byte[] { 5, 0 }, cancellationToken);
        var request = await ReadExactAsync(stream, 4, cancellationToken);
        await ConsumeSocksAddressAsync(stream, request[3], cancellationToken);
        await stream.WriteAsync(
            new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 0 }, cancellationToken);
        var headers = await ReadHeadersAsync(stream, cancellationToken);
        await WriteNoContentAsync(stream, cancellationToken);
        return headers;
    }

    private static async Task ConsumeSocksAddressAsync(
        Stream stream,
        byte addressType,
        CancellationToken cancellationToken)
    {
        var addressLength = addressType switch
        {
            1 => 4,
            4 => 16,
            3 => (await ReadExactAsync(stream, 1, cancellationToken))[0],
            _ => throw new InvalidDataException("Unsupported SOCKS address type.")
        };
        await ReadExactAsync(stream, addressLength + 2, cancellationToken);
    }

    private static async Task<byte[]> ReadExactAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task<string> ReadHeadersAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        while (buffer.Count < 8192)
        {
            var value = await ReadExactAsync(stream, 1, cancellationToken);
            buffer.Add(value[0]);
            if (buffer.Count >= 4
                && buffer[buffer.Count - 4] == '\r'
                && buffer[buffer.Count - 3] == '\n'
                && buffer[buffer.Count - 2] == '\r'
                && buffer[buffer.Count - 1] == '\n')
            {
                return Encoding.ASCII.GetString(buffer.ToArray());
            }
        }

        throw new InvalidDataException("HTTP headers exceeded test limit.");
    }

    private static Task WriteNoContentAsync(
        Stream stream,
        CancellationToken cancellationToken) =>
        stream.WriteAsync(
            "HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
            cancellationToken).AsTask();
}
