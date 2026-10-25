using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class McpRevisionMigrationFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));

    private McpRevisionMigrationFixture()
    {
        var paths = new AppDataPaths(_root);
        Connections = new SqliteConnectionFactory(paths);
        Initializer = new SqliteDatabaseInitializer(paths, Connections);
        Repository = new SqliteMcpConfigurationRepository(Connections);
    }

    public CancellationToken Token => TestContext.Current.CancellationToken;
    public SqliteConnectionFactory Connections { get; }
    public SqliteDatabaseInitializer Initializer { get; }
    public SqliteMcpConfigurationRepository Repository { get; }

    public static async Task<McpRevisionMigrationFixture> CreateAsync()
    {
        var fixture = new McpRevisionMigrationFixture();
        try
        {
            await fixture.Initializer.InitializeAsync(fixture.Token);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = Connections.Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Token);
    }

    public async Task<long> CountAsync(string sql)
    {
        await using var connection = Connections.Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token));
    }

    public ValueTask DisposeAsync()
    {
        SqliteTestPool.Clear(_root);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return ValueTask.CompletedTask;
    }
}
