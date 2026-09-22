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
}
