using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v52: the explicit trust revision counter. plugin_packages gains a
/// monotonically increasing revision bumped on every real state transition, and
/// task_skill_activations snapshots the revision the dispatch ran under. Existing rows get 1 —
/// their transition history is unknown, so 1 is the honest baseline. Column additions have no
/// IF NOT EXISTS form, so each column's presence is its own gate: a replay after losing only
/// the version row must not fail on an existing column. The version row alone cannot be the
/// whole early-out: the v50 plugin_packages rebuild drops this column while keeping the v52
/// version row, so a surviving version row with a missing column repairs the column instead of
/// skipping — the version insert stays gated on the row's own absence.</summary>
internal static class PluginTrustRevisionMigration
{
    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=52;";
        var versioned = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('plugin_packages') WHERE name='trust_revision';";
        var pluginColumn = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('task_skill_activations') WHERE name='trust_revision';";
        var activationColumn = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0;
        if (versioned && pluginColumn && activationColumn) return;
        if (!pluginColumn)
        {
            command.CommandText = "ALTER TABLE plugin_packages ADD COLUMN trust_revision INTEGER NOT NULL DEFAULT 1;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        if (!activationColumn)
        {
            command.CommandText = "ALTER TABLE task_skill_activations ADD COLUMN trust_revision INTEGER NOT NULL DEFAULT 1;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        if (!versioned)
        {
            command.CommandText = "INSERT INTO schema_versions(version,applied_at) VALUES(52,datetime('now'));";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
