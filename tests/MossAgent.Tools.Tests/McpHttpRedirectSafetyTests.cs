using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpRedirectSafetyTests
{
    [Fact]
    public async Task ConfiguredHeaders_AreNotForwardedToAnUnconfiguredRedirectTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var token = TestContext.Current.CancellationToken;
        await using var destination = new McpHttpFixtureServer();
        await using var source = new McpHttpFixtureServer { RedirectUrl = destination.Endpoint };
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(token);
            var configurations = new SqliteMcpConfigurationRepository(connections);
            var proxies = new SqliteConfigurationRepository(connections);
            var tools = new ToolRegistry(Array.Empty<IAgentTool>());
            await configurations.SaveAsync(new McpServerProfile(Guid.NewGuid(), "redirect", McpTransportKind.StreamableHttp,
                null, "[]", null, "{}", source.Endpoint,
                "{\"X-Moss-Fixture\":\"enabled\",\"Authorization\":\"Bearer synthetic-redirection-secret\"}", null, true), token);
            await using var manager = new McpManager(configurations, new McpHttpClientFactory(proxies), tools,
                new RejectingTaskProcessLauncher());

            await manager.ReloadAsync(token);

            Assert.True(source.CustomHeaderObserved);
            Assert.False(destination.CustomHeaderObserved);
            Assert.Empty(tools.Descriptors);
            Assert.DoesNotContain("synthetic-redirection-secret", Assert.Single(manager.Errors).Value, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
