using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v45: explicit per-grant write authorization for source repositories.
/// Column additions have no IF NOT EXISTS form, so the column presence itself is the
/// second gate: a replay after losing only the version row must not fail on the existing
/// column. Existing grants keep can_write=0 and stay read-only; write is opt-in at grant
/// creation only and never mutates an existing row.</summary>
internal static class SourceRepositoryGrantWriteMigration
{
    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=45;";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0) return;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('source_repository_grants') WHERE name='can_write';";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            command.CommandText = "ALTER TABLE source_repository_grants ADD COLUMN can_write INTEGER NOT NULL DEFAULT 0;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        command.CommandText = "INSERT INTO schema_versions(version,applied_at) VALUES(45,datetime('now'));";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
