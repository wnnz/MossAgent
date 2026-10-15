using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpManagerLifecycleTests
{
    [Fact]
    public async Task Reload_HidesRemoteErrorDetailsAndDisposesFailedSession()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        const string secret = "fixture-private-token";
        await using var server = new McpHttpFixtureServer { InitializeError = secret };
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections)
                .InitializeAsync(cancellationToken);
            var configurations = new SqliteMcpConfigurationRepository(connections);
            var proxies = new SqliteConfigurationRepository(connections);
            var tools = new ToolRegistry(Array.Empty<IAgentTool>());
            await using var manager = new McpManager(
                configurations, new McpHttpClientFactory(proxies), tools, new RejectingTaskProcessLauncher());
            await configurations.SaveAsync(CreateHttpProfile(server.Endpoint), cancellationToken);

            await manager.ReloadAsync(cancellationToken);

            var error = Assert.Single(manager.Errors).Value;
            Assert.DoesNotContain(secret, error);
            Assert.True(server.DeleteObserved);
            Assert.DoesNotContain(tools.Descriptors, IsMcpTool);

            server.InitializeError = null;
            await manager.ReloadAsync(cancellationToken);
            Assert.Empty(manager.Errors);
            Assert.Contains(tools.Descriptors, IsMcpTool);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reload_TimesOutUnresponsiveServerAndPropagatesCallerCancellation()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = new McpHttpFixtureServer
        {
            InitializeDelay = TimeSpan.FromSeconds(30)
        };
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections)
                .InitializeAsync(cancellationToken);
            var configurations = new SqliteMcpConfigurationRepository(connections);
            var proxies = new SqliteConfigurationRepository(connections);
            var tools = new ToolRegistry(Array.Empty<IAgentTool>());
            await using var manager = new McpManager(
                configurations, new McpHttpClientFactory(proxies), tools,
                new RejectingTaskProcessLauncher(), TimeSpan.FromMilliseconds(200));
            await configurations.SaveAsync(CreateHttpProfile(server.Endpoint), cancellationToken);

            await manager.ReloadAsync(cancellationToken);

            Assert.Contains("超时", Assert.Single(manager.Errors).Value);
            Assert.DoesNotContain(tools.Descriptors, IsMcpTool);

            using var callerCancellation = new CancellationTokenSource();
            callerCancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => manager.ReloadAsync(callerCancellation.Token));
            Assert.Empty(manager.Errors);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool IsMcpTool(ToolDescriptor tool) =>
        tool.Name.StartsWith("mcp.", StringComparison.Ordinal);

    private static McpServerProfile CreateHttpProfile(Uri endpoint) => new(
        Guid.NewGuid(), "fixture", McpTransportKind.StreamableHttp,
        null, "[]", null, "{}", endpoint, "{}", null, true);

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

}
