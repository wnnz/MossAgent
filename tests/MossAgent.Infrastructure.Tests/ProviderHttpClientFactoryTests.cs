using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Networking;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

[Collection("ProviderHttpClientFactory")]
public sealed class ProviderHttpClientFactoryTests
{
    [Fact]
    public async Task CreateAsync_RoutesProviderRequestThroughConfiguredProxy()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var listener = StartListener(out var proxyPort);
            var cancellationToken = timeout.Token;
            var proxyRequest = ReceiveProxyRequestAsync(listener, cancellationToken);
            var repository = await CreateRepositoryAsync(root, cancellationToken);
            var proxy = new ProxyProfile(
                Guid.NewGuid(), "local-fixture", ProxyProtocol.Http,
                "127.0.0.1", proxyPort, null, null, false, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("http://models.example.test/v1/"), "fixture-key",
                proxy.Id, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);

            using var client = await factory.CreateAsync(provider, cancellationToken);
            using var response = await client.GetAsync("models", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                "GET http://models.example.test/v1/models HTTP/1.1",
                await proxyRequest);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_UsesCredentialsOnlyForAuthenticatedProxy()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var listener = StartListener(out var proxyPort);
            var cancellationToken = timeout.Token;
            var proxyAuthorization = ReceiveAuthenticatedProxyRequestAsync(listener, cancellationToken);
            var repository = await CreateRepositoryAsync(root, cancellationToken);
            var proxy = new ProxyProfile(
                Guid.NewGuid(), "authenticated-fixture", ProxyProtocol.Http,
                "127.0.0.1", proxyPort, "fixture-user", "fixture-password", false, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("http://models.example.test/v1/"), "provider-key",
                proxy.Id, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);

            using var client = await factory.CreateAsync(provider, cancellationToken);
            using var response = await client.GetAsync("models", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var expected = "Basic " + Convert.ToBase64String(
                Encoding.ASCII.GetBytes("fixture-user:fixture-password"));
            Assert.True(string.Equals(expected, await proxyAuthorization, StringComparison.Ordinal),
                "The configured proxy credentials were not applied to proxy authentication.");
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_UsesDirectConnectionWhenNoProxyIsSelected()
    {
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var previousProxy = HttpClient.DefaultProxy;
        using var proxyListener = StartListener(out var proxyPort);
        using var targetListener = StartListener(out var targetPort);
        using var proxyCancellation = new CancellationTokenSource();
        var proxyRequest = ReceiveProxyRequestAsync(proxyListener, proxyCancellation.Token);
        var targetRequest = ReceiveProxyRequestAsync(targetListener, timeout.Token);
        try
        {
            HttpClient.DefaultProxy = new WebProxy($"http://127.0.0.1:{proxyPort}")
            {
                BypassProxyOnLocal = false,
                BypassList = Array.Empty<string>()
            };
            var repository = await CreateRepositoryAsync(root, timeout.Token);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri($"http://127.0.0.1:{targetPort}/v1/"), "fixture-key",
                null, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);

            using var client = await factory.CreateAsync(provider, timeout.Token);
            using var response = await client.GetAsync("models", timeout.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("GET /v1/models HTTP/1.1", await targetRequest);
            Assert.False(proxyRequest.IsCompleted);
        }
        finally
        {
            proxyCancellation.Cancel();
            try { await proxyRequest; } catch (OperationCanceledException) { }
            HttpClient.DefaultProxy = previousProxy;
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

    private static async Task<string> ReceiveProxyRequestAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
        {
        }

        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
        await stream.WriteAsync(response.AsMemory(), cancellationToken);
        return requestLine;
    }

    private static async Task<string> ReceiveAuthenticatedProxyRequestAsync(
        TcpListener listener,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            string? authorization = null;
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
            {
                if (line.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                    authorization = line["Proxy-Authorization:".Length..].Trim();
            }

            var response = authorization is null
                ? Encoding.ASCII.GetBytes(
                    "HTTP/1.1 407 Proxy Authentication Required\r\n"
                    + "Proxy-Authenticate: Basic realm=\"fixture\"\r\n"
                    + "Content-Length: 0\r\nConnection: close\r\n\r\n")
                : Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
            await stream.WriteAsync(response.AsMemory(), cancellationToken);
            if (authorization is not null)
                return authorization;
        }

        return string.Empty;
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
