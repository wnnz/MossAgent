using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.App.Tests;

internal static class WorkspaceWorktreeRecoverySeeding
{
    public static async Task<Guid> SeedMissingOrphanAsync(WorkspaceModelSelectionFixture fixture,
        ProjectProfile project)
    {
        var connections = new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot));
        var intents = new SqliteWorktreeCreationIntents(connections);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.DataRoot, "worktrees", taskId.ToString("N"));
        await intents.RecordAsync(taskId, project, project.PrimaryDirectory,
            null, "seed", target, fixture.Token);
        await ExecuteAsync(connections, """
            UPDATE worktree_creation_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """, fixture.Token);
        await ((IWorktreeCreationIntents)fixture.Worktrees).ReconcileAsync(fixture.Token);
        return taskId;
    }

    private static async Task ExecuteAsync(SqliteConnectionFactory connections, string commandText,
        CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(token);
    }
}
