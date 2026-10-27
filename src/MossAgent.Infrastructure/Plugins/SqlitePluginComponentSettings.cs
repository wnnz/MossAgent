using Microsoft.Data.Sqlite;
using MossAgent.Application.Plugins;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>SQLite store for per-skill enable choices. Writes upsert the (plugin, component) row
/// in a single statement and stamp it with the store clock; reads return the stored flag while an
/// absent row reports the default-enabled state. Rows belong to the plugin_packages parent and
/// cascade with it, so a reinstall under a new id starts from defaults.</summary>
public sealed class SqlitePluginComponentSettings(SqliteConnectionFactory connections,
    TimeProvider? timeProvider = null) : IPluginComponentSettings
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<bool> IsSkillEnabledAsync(Guid pluginId, string componentPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_enabled FROM plugin_component_settings "
            + "WHERE plugin_id=$plugin AND component_path=$path;";
        command.Parameters.AddWithValue("$plugin", pluginId.ToString());
        command.Parameters.AddWithValue("$path", componentPath);
        var stored = await command.ExecuteScalarAsync(cancellationToken);
        return stored is null or DBNull || Convert.ToInt64(stored) != 0;
    }

    public async Task SetSkillEnabledAsync(Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO plugin_component_settings(plugin_id,component_path,is_enabled,updated_at)
            VALUES($plugin,$path,$enabled,$at)
            ON CONFLICT(plugin_id,component_path)
            DO UPDATE SET is_enabled=$enabled,updated_at=$at;
            """;
        command.Parameters.AddWithValue("$plugin", pluginId.ToString());
        command.Parameters.AddWithValue("$path", componentPath);
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        command.Parameters.AddWithValue("$at", _time.GetUtcNow().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, bool>> ListOverridesAsync(Guid pluginId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT component_path,is_enabled FROM plugin_component_settings "
            + "WHERE plugin_id=$plugin ORDER BY component_path;";
        command.Parameters.AddWithValue("$plugin", pluginId.ToString());
        var overrides = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            overrides[reader.GetString(0)] = reader.GetInt64(1) != 0;
        return overrides;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
