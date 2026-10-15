using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class SqliteFailurePersistenceFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));

    private SqliteFailurePersistenceFixture()
    {
        var paths = new AppDataPaths(_root);
        Connections = new SqliteConnectionFactory(paths);
        Initializer = new SqliteDatabaseInitializer(paths, Connections);
        Repository = new SqliteWorkspaceRepository(Connections);
        Project = new ProjectProfile(Guid.NewGuid(), "failure fixture", _root, [_root], DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        Task = new AgentTask(Guid.NewGuid(), Project.Id, "legacy failed task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Failed, now, now, null);
    }

    public CancellationToken Token => TestContext.Current.CancellationToken;
    public SqliteConnectionFactory Connections { get; }
    public SqliteDatabaseInitializer Initializer { get; }
    public SqliteWorkspaceRepository Repository { get; }
    public ProjectProfile Project { get; }
    public AgentTask Task { get; }

    public static async Task<SqliteFailurePersistenceFixture> CreateAsync()
    {
        var fixture = new SqliteFailurePersistenceFixture();
        try
        {
            await fixture.Initializer.InitializeAsync(fixture.Token);
            await fixture.Repository.SaveProjectAsync(fixture.Project, fixture.Token);
            await fixture.Repository.SaveTaskAsync(fixture.Task, fixture.Token);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
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
