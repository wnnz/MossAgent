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
        await StaleAndReconcileAsync(connections, "worktree_creation_intents", fixture);
        return taskId;
    }

    public static async Task<Guid> SeedRemovedCleanupOrphanAsync(WorkspaceModelSelectionFixture fixture,
        ProjectProfile project)
    {
        var connections = new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot));
        var intents = new SqliteWorktreeCleanupIntents(connections);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.DataRoot, "worktrees", taskId.ToString("N"));
        await intents.RecordAsync(taskId, project, project.PrimaryDirectory, target, fixture.Token);
        await StaleAndReconcileAsync(connections, "worktree_cleanup_intents", fixture);
        return taskId;
    }

    private static async Task StaleAndReconcileAsync(SqliteConnectionFactory connections, string table,
        WorkspaceModelSelectionFixture fixture)
    {
        await ExecuteAsync(connections, $"""
            UPDATE {table} SET updated_at='2000-01-01T00:00:00.0000000+00:00' WHERE stage<3;
            """, fixture.Token);
        await ((IWorktreeCreationIntents)fixture.Worktrees).ReconcileAsync(fixture.Token);
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
