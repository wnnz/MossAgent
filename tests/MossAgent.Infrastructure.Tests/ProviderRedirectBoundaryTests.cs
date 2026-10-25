using System.Net;
using System.Net.Sockets;
using System.Text;
using MossAgent.Domain;
using MossAgent.Infrastructure.Networking;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

[Collection("ProviderHttpClientFactory")]
public sealed class ProviderRedirectBoundaryTests
{
    [Fact]
    public async Task CreateAsync_DoesNotFollowRedirectsOrForwardHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var cancellationToken = timeout.Token;
        try
        {
            using var leakListener = StartListener(out var leakPort);
            using var server = StartListener(out var serverPort);
            var leaked = ReceiveAnyRequestAsync(leakListener, cancellationToken);
            var answered = AnswerRedirectAsync(server, leakPort, cancellationToken);

            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            var repository = new SqliteConfigurationRepository(connections);
            var provider = new AiProvider(
                Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri($"http://127.0.0.1:{serverPort}/v1/"), "fixture-key",
                null, false, true, new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer fixture-key"
                });
            var factory = new ProviderHttpClientFactory(repository);

            using var client = await factory.CreateAsync(provider, cancellationToken);
            using var response = await client.GetAsync("models", cancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            await answered;
            Assert.False(leaked.IsCompleted,
                "The client followed a redirect and would have forwarded provider headers.");
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static TcpListener StartListener(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static async Task AnswerRedirectAsync(TcpListener listener, int leakPort,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
        {
        }

        var response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{leakPort}/leak\r\n"
            + "Content-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response.AsMemory(), cancellationToken);
    }

    private static async Task ReceiveAnyRequestAsync(TcpListener listener,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        using var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
        await reader.ReadLineAsync(cancellationToken);
    }
}
