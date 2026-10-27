using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v50 migration runner. The state-check widening requires a plugin_packages
/// rebuild, and plugin_component_settings rows reference it with ON DELETE CASCADE, so foreign
/// keys must be off while the table is swapped — otherwise the drop would delete the choices.
/// The rebuild copies every row back before commit and foreign_key_check runs inside the
/// transaction, so a replayed migration after losing only the version row keeps all data.</summary>
internal static class PluginComponentHashMigration
{
    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys=OFF;";
        try
        {
            await foreignKeys.ExecuteNonQueryAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=50;";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0) return;
            command.CommandText = PluginComponentHashDatabaseSchema.VersionFifty;
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "PRAGMA foreign_key_check;";
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("Workspace migration failed foreign-key validation.");
            }
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys=ON;";
            await foreignKeys.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
