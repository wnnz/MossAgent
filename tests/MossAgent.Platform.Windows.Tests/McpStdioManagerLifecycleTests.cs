using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class McpStdioManagerLifecycleTests
{
    [Fact]
    public async Task Reload_TracksEnabledUpdatedAndDeletedServers()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            var configurations = new SqliteMcpConfigurationRepository(connections);
            var proxies = new SqliteConfigurationRepository(connections);
            var tools = new ToolRegistry(Array.Empty<IAgentTool>());
            await using var manager = new McpManager(configurations, new McpHttpClientFactory(proxies), tools,
                new WindowsTaskProcessLauncher());
            var profile = CreateProfile(root);
            var saved = await configurations.SaveAsync(profile, cancellationToken);
            await manager.ReloadAsync(cancellationToken);
            Assert.Contains(tools.Descriptors, tool => tool.Name.StartsWith($"mcp.fixture.{profile.Id:N}.echo__", StringComparison.Ordinal));

            saved = await configurations.SaveAsync(saved with { IsEnabled = false }, cancellationToken);
            await manager.ReloadAsync(cancellationToken);
            Assert.DoesNotContain(tools.Descriptors, IsMcpTool);

            saved = await configurations.SaveAsync(saved with { IsEnabled = true }, cancellationToken);
            await manager.ReloadAsync(cancellationToken);
            await configurations.DeleteAsync(profile.Id, cancellationToken);
            await manager.ReloadAsync(cancellationToken);
            Assert.DoesNotContain(tools.Descriptors, IsMcpTool);
            Assert.Empty(await configurations.GetAllAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            await WindowsProcessTestDirectory.DeleteAsync(root);
        }
    }

    private static bool IsMcpTool(ToolDescriptor tool) => tool.Name.StartsWith("mcp.", StringComparison.Ordinal);

    private static McpServerProfile CreateProfile(string root)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var script = Path.Combine(directory.FullName, "tests", "fixtures", "mcp-echo-server.ps1");
        return new McpServerProfile(Guid.NewGuid(), "fixture", McpTransportKind.Stdio, "pwsh",
            JsonSerializer.Serialize(new[] { "-NoProfile", "-File", script }), root, "{}", null, "{}", null, true);
    }
}
