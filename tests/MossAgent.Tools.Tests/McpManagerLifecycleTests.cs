using System.Text.Json;
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
    public async Task Reload_TracksEnabledUpdatedAndDeletedServers()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections)
                .InitializeAsync(cancellationToken);
            var configurations = new SqliteMcpConfigurationRepository(connections);
            var proxies = new SqliteConfigurationRepository(connections);
            var tools = new ToolRegistry(Array.Empty<IAgentTool>());
            var manager = new McpManager(
                configurations, new McpHttpClientFactory(proxies), tools);
            await using (manager)
            {
                var profile = CreateProfile(root);
                await configurations.SaveAsync(profile, cancellationToken);
                await manager.ReloadAsync(cancellationToken);
                Assert.Contains(tools.Descriptors, static tool => tool.Name == "mcp.fixture.echo");

                await configurations.SaveAsync(
                    profile with { IsEnabled = false }, cancellationToken);
                await manager.ReloadAsync(cancellationToken);
                Assert.DoesNotContain(tools.Descriptors, IsMcpTool);

                await configurations.SaveAsync(profile, cancellationToken);
                await manager.ReloadAsync(cancellationToken);
                await configurations.DeleteAsync(profile.Id, cancellationToken);
                await manager.ReloadAsync(cancellationToken);
                Assert.DoesNotContain(tools.Descriptors, IsMcpTool);
                Assert.Empty(await configurations.GetAllAsync(cancellationToken));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool IsMcpTool(ToolDescriptor tool) =>
        tool.Name.StartsWith("mcp.", StringComparison.Ordinal);

    private static McpServerProfile CreateProfile(string root)
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = Path.Combine(repositoryRoot, "tests", "fixtures", "mcp-echo-server.ps1");
        return new McpServerProfile(
            Guid.NewGuid(), "fixture", McpTransportKind.Stdio, "pwsh",
            JsonSerializer.Serialize(new[] { "-NoProfile", "-File", script }),
            root, "{}", null, "{}", null, true);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MossAgent.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
