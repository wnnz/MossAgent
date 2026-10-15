using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class McpStdioManagerFixture : IAsyncDisposable
{
    private McpStdioManagerFixture(string mode)
    {
        Server = new McpStdioProcessFixture(mode);
        var paths = new AppDataPaths(Path.Combine(Server.Root, "data"));
        var connections = new SqliteConnectionFactory(paths);
        Initializer = new SqliteDatabaseInitializer(paths, connections);
        Configurations = new SqliteMcpConfigurationRepository(connections);
        Tools = new ToolRegistry(Array.Empty<IAgentTool>());
        Manager = new McpManager(Configurations,
            new McpHttpClientFactory(new SqliteConfigurationRepository(connections)), Tools,
            new WindowsTaskProcessLauncher(), TimeSpan.FromSeconds(3), Clock);
    }

    private SqliteDatabaseInitializer Initializer { get; }
    public McpStdioProcessFixture Server { get; }
    public SqliteMcpConfigurationRepository Configurations { get; }
    public ToolRegistry Tools { get; }
    public McpManager Manager { get; }
    public McpManagerTestClock Clock { get; } = new();

    public static async Task<McpStdioManagerFixture> CreateAsync(string mode = "echo")
    {
        var fixture = new McpStdioManagerFixture(mode);
        try
        {
            await fixture.Initializer.InitializeAsync(fixture.Server.Token);
            await fixture.Configurations.SaveAsync(fixture.Server.Profile, fixture.Server.Token);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Manager.DisposeAsync(); }
        finally
        {
            SqliteConnection.ClearAllPools();
            await Server.DisposeAsync();
        }
    }
}
