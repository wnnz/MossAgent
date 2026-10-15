using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Networking;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProviderHttpClientFactorySocks5Tests
{
    [Fact]
    public async Task CreateAsync_RoutesProviderRequestThroughConfiguredSocks5Proxy()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var listener = StartListener(out var proxyPort);
            var proxyRequest = ServeSocks5ProxyAsync(listener, timeout.Token);
            var repository = await CreateRepositoryAsync(root, timeout.Token);
            var proxy = new ProxyProfile(
                Guid.NewGuid(), "socks-fixture", ProxyProtocol.Socks5,
                "127.0.0.1", proxyPort, null, null, false, true);
            await repository.SaveProxyAsync(proxy, timeout.Token);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("http://models.example.test/v1/"), "fixture-key",
                proxy.Id, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);

            using var client = await factory.CreateAsync(provider, timeout.Token);
            using var response = await client.GetAsync("models", timeout.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("GET /v1/models HTTP/1.1", await proxyRequest);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<SqliteConfigurationRepository> CreateRepositoryAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var paths = new AppDataPaths(root);
        var connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
        return new SqliteConfigurationRepository(connections);
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static async Task<string> ServeSocks5ProxyAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        await Socks5TestHandshake.AcceptAsync(stream, "models.example.test", 80, cancellationToken);
        var headers = await ReadHeadersAsync(stream, cancellationToken);
        await stream.WriteAsync(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}"u8.ToArray(),
            cancellationToken);
        return headers;
    }

    private static async Task<byte[]> ReadExactAsync(
        Stream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }

    private static async Task<string> ReadHeadersAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        while (buffer.Count < 8192)
        {
            buffer.Add((await ReadExactAsync(stream, 1, cancellationToken))[0]);
            if (buffer.Count >= 4
                && buffer[^4] == '\r' && buffer[^3] == '\n'
                && buffer[^2] == '\r' && buffer[^1] == '\n')
            {
                return Encoding.ASCII.GetString(buffer.ToArray());
            }
        }

        throw new InvalidDataException("HTTP headers exceeded test limit.");
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
