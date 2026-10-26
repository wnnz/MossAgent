using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v42: pins the authorization snapshot generation each claimed run executed
/// under. Column additions have no IF NOT EXISTS form, so the column presence itself is the
/// second gate: a replay after losing only the version row must not fail on the existing
/// column. Existing rows keep NULL; their generation is unknown and never backfilled.</summary>
internal static class AutomationRunGenerationMigration
{
    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=42;";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0) return;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('automation_runs') WHERE name='snapshot_generation';";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            command.CommandText = "ALTER TABLE automation_runs ADD COLUMN snapshot_generation INTEGER;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        command.CommandText = "INSERT INTO schema_versions(version,applied_at) VALUES(42,datetime('now'));";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
