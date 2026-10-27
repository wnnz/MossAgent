using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Networking;
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

    [Fact]
    public async Task CreateAsync_ConcurrentProxyEditResolvesCoherentCredentials()
    {
        const int rounds = 10;
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var listener = StartListener(out var proxyPort);
            var cancellationToken = timeout.Token;
            var repository = await CreateRepositoryAsync(root, cancellationToken);
            var proxy = new ProxyProfile(
                Guid.NewGuid(), "race-fixture", ProxyProtocol.Http,
                "127.0.0.1", proxyPort, "user-one", "pass-one", false, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("http://models.example.test/v1/"), "fixture-key",
                proxy.Id, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);
            var authorizations = new ConcurrentQueue<string>();
            var accept = Task.Run(async () =>
            {
                for (var i = 0; i < rounds; i++)
                    authorizations.Enqueue(await ReceiveAuthenticatedProxyRequestAsync(listener, cancellationToken));
            }, CancellationToken.None);
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("user-one:pass-one")),
                "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("user-two:pass-two")),
            };

            for (var i = 0; i < rounds; i++)
            {
                var save = repository.SaveProxyAsync(proxy with
                {
                    Username = i % 2 == 0 ? "user-one" : "user-two",
                    Password = i % 2 == 0 ? "pass-one" : "pass-two",
                }, cancellationToken);
                using var client = await factory.CreateAsync(provider, cancellationToken);
                using var response = await client.GetAsync("models", cancellationToken);
                await save;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            await accept;
            Assert.Equal(rounds, authorizations.Count);
            Assert.All(authorizations, value => Assert.Contains(value, expected));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_ConcurrentProxyDisableStaysFailClosed()
    {
        const int rounds = 8;
        var root = CreateRoot();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var listener = StartListener(out var proxyPort);
            var cancellationToken = timeout.Token;
            var repository = await CreateRepositoryAsync(root, cancellationToken);
            var proxy = new ProxyProfile(
                Guid.NewGuid(), "disable-race", ProxyProtocol.Http,
                "127.0.0.1", proxyPort, null, null, false, true);
            await repository.SaveProxyAsync(proxy, cancellationToken);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("http://models.example.test/v1/"), "fixture-key",
                proxy.Id, false, true, new Dictionary<string, string>());
            var factory = new ProviderHttpClientFactory(repository);
            var requests = new ConcurrentQueue<string>();
            using var acceptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var accept = Task.Run(async () =>
            {
                try
                {
                    while (true)
                        requests.Enqueue(await ReceiveProxyRequestAsync(listener, acceptCancellation.Token));
                }
                catch (OperationCanceledException) { }
            }, CancellationToken.None);
            var succeeded = 0;
            var unavailable = 0;

            for (var i = 0; i < rounds; i++)
            {
                var save = repository.SaveProxyAsync(proxy with { IsEnabled = i % 2 == 0 }, cancellationToken);
                try
                {
                    using var client = await factory.CreateAsync(provider, cancellationToken);
                    using var response = await client.GetAsync("models", cancellationToken);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    succeeded++;
                }
                catch (ProviderProxyUnavailableException)
                {
                    unavailable++;
                }
                await save;
            }

            Assert.Equal(rounds, succeeded + unavailable);
            Assert.True(succeeded > 0 && unavailable > 0,
                $"Expected the toggling edit to produce both snapshots, got {succeeded}/{unavailable}.");
            acceptCancellation.Cancel();
            await accept;
            Assert.Equal(succeeded, requests.Count);
            Assert.All(requests, value => Assert.Equal("GET http://models.example.test/v1/models HTTP/1.1", value));
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
