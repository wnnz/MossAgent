using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer(
    AppDataPaths paths,
    SqliteConnectionFactory connections) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(DatabaseSchema.VersionOne, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var versionTwo = new SqliteCommand(DatabaseSchema.VersionTwo, connection);
        await versionTwo.ExecuteNonQueryAsync(cancellationToken);
        await using var versionThree = new SqliteCommand(DatabaseSchema.VersionThree, connection);
        await versionThree.ExecuteNonQueryAsync(cancellationToken);
        await using var versionFour = new SqliteCommand(DatabaseSchema.VersionFour, connection);
        await versionFour.ExecuteNonQueryAsync(cancellationToken);
        await using var versionFive = new SqliteCommand(DatabaseSchema.VersionFive, connection);
        await versionFive.ExecuteNonQueryAsync(cancellationToken);
        await using var versionSix = new SqliteCommand(DatabaseSchema.VersionSix, connection);
        await versionSix.ExecuteNonQueryAsync(cancellationToken);
        await using var versionSeven = new SqliteCommand(DatabaseSchema.VersionSeven, connection);
        await versionSeven.ExecuteNonQueryAsync(cancellationToken);
        await ApplyVersionAsync(connection, 8, DatabaseSchema.VersionEight, cancellationToken);
        await ApplyVersionNineAsync(connection, cancellationToken);
        await ApplyVersionAsync(connection, 10, TaskFailureDatabaseSchema.VersionTen, cancellationToken);
        await ApplyVersionAsync(connection, 11, ConversationPreferenceDatabaseSchema.VersionEleven, cancellationToken);
        await ProjectlessChatMigration.ApplyAsync(connection, cancellationToken);

        await using var recover = connection.CreateCommand();
        recover.CommandText = """
            UPDATE agent_tasks
            SET status = $interrupted, updated_at = $updated
            WHERE status IN ($running, $waiting);
            """;
        recover.Parameters.AddWithValue("$interrupted", (int)Domain.AgentTaskStatus.Interrupted);
        recover.Parameters.AddWithValue("$running", (int)Domain.AgentTaskStatus.Running);
        recover.Parameters.AddWithValue("$waiting", (int)Domain.AgentTaskStatus.WaitingForApproval);
        recover.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await recover.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyVersionNineAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = AutomationDatabaseSchema.VersionNine;
        await migration.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyVersionAsync(SqliteConnection connection, int version, string schema,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=$version;";
        existing.Parameters.AddWithValue("$version", version);
        if (Convert.ToInt64(await existing.ExecuteScalarAsync(cancellationToken)) != 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        await using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = schema;
        await migration.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
