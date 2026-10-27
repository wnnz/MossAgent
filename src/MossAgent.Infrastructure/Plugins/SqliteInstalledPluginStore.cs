using System.Globalization;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>SQLite store for installed plugin rows. All reads and the compare-and-swap insert
/// run in single statements or one transaction; a UNIQUE(name,version) violation is resolved by
/// re-reading the committed row — same content hash means an idempotent reinstall, anything
/// else is a conflict. Per-component payload hashes are written in the same transaction as
/// their plugin row and only ever inserted, never replaced, so the first recorded baseline
/// stays authoritative.</summary>
public sealed class SqliteInstalledPluginStore(SqliteConnectionFactory connections) : IInstalledPluginStore
{
    private const string Columns =
        "id,name,version,content_hash,install_path,state,source_path,installed_at,components_json,declared_mcp_json,overlay_source";

    public async Task<InstalledPlugin?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await FindByIdAsync(connection, null, id, cancellationToken);
    }

    public async Task<InstalledPlugin?> GetByNameVersionAsync(string name, string version,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await FindAsync(connection, null, name, version, cancellationToken);
    }

    public async Task<InstalledPlugin> InsertAsync(InstalledPlugin plugin,
        IReadOnlyDictionary<string, string> payloadHashes, CancellationToken cancellationToken)
    {
        var components = InstalledPluginJson.SerializeComponents(plugin.Components);
        var servers = InstalledPluginJson.SerializeServers(plugin.DeclaredMcpServers);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var existing = await FindAsync(connection, transaction, plugin.Name, plugin.Version, cancellationToken);
        if (existing is not null)
        {
            if (!SameContent(existing, plugin)) throw Conflict(plugin);
            await InsertHashesAsync(connection, transaction, existing.Id, payloadHashes, true,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO plugin_packages({Columns}) "
            + "VALUES($id,$name,$version,$hash,$path,$state,$source,$at,$components,$servers,$overlay);";
        AddParameters(command, plugin, components, servers);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException error) when (error.SqliteErrorCode == 19)
        {
            var raced = await FindAsync(connection, transaction, plugin.Name, plugin.Version, cancellationToken);
            if (raced is not null)
            {
                if (!SameContent(raced, plugin)) throw Conflict(plugin);
                await InsertHashesAsync(connection, transaction, raced.Id, payloadHashes, true,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return raced;
            }
            throw new PluginPackageException(PluginPackageErrorCode.InstallFailed,
                "the plugin row violated the storage schema", error);
        }
        await InsertHashesAsync(connection, transaction, plugin.Id, payloadHashes, false,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return plugin;
    }

    public async Task<InstalledPlugin?> SetStateAsync(Guid id, InstalledPluginState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var row = await FindByIdAsync(connection, transaction, id, cancellationToken);
        if (row is null) return null;
        if (row.State != state)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE plugin_packages SET state=$state WHERE id=$id;";
            command.Parameters.AddWithValue("$state", (int)state);
            command.Parameters.AddWithValue("$id", id.ToString());
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
            row = row with { State = state };
        }
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    public async Task<InstalledPlugin?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var row = await FindByIdAsync(connection, transaction, id, cancellationToken);
        if (row is null) return null;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM plugin_packages WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    public async Task<IReadOnlyList<InstalledPlugin>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM plugin_packages ORDER BY name,version;";
        var plugins = new List<InstalledPlugin>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            plugins.Add(Map(reader));
        return plugins;
    }

    public async Task<IReadOnlyDictionary<string, string>> ListPayloadHashesAsync(Guid pluginId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT component_path,sha256 FROM plugin_component_hashes "
            + "WHERE plugin_id=$plugin ORDER BY component_path;";
        command.Parameters.AddWithValue("$plugin", pluginId.ToString());
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            hashes[reader.GetString(0)] = reader.GetString(1);
        return hashes;
    }

    public async Task SavePayloadHashesAsync(Guid pluginId, IReadOnlyDictionary<string, string> hashes,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await InsertHashesAsync(connection, transaction, pluginId, hashes, true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<InstalledPlugin?> FindByIdAsync(SqliteConnection connection,
        SqliteTransaction? transaction, Guid id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM plugin_packages WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static async Task<InstalledPlugin?> FindAsync(SqliteConnection connection,
        SqliteTransaction? transaction, string name, string version, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM plugin_packages WHERE name=$name AND version=$version;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$version", version);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    /// <summary>Writes hash rows inside the caller's transaction. <paramref name="orIgnore"/>
    /// keeps an already-established baseline authoritative — reruns and races only fill missing
    /// keys. A fresh plugin row uses a strict insert instead: any invalid hash row fails the
    /// transaction so the plugin row is never committed without its complete baseline.</summary>
    private static async Task InsertHashesAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid pluginId, IReadOnlyDictionary<string, string> hashes, bool orIgnore,
        CancellationToken cancellationToken)
    {
        foreach (var (path, hash) in hashes)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = (orIgnore ? "INSERT OR IGNORE" : "INSERT")
                + " INTO plugin_component_hashes(plugin_id,component_path,sha256) "
                + "VALUES($plugin,$path,$hash);";
            command.Parameters.AddWithValue("$plugin", pluginId.ToString());
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$hash", hash);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddParameters(SqliteCommand command, InstalledPlugin plugin,
        string components, string servers)
    {
        command.Parameters.AddWithValue("$id", plugin.Id.ToString());
        command.Parameters.AddWithValue("$name", plugin.Name);
        command.Parameters.AddWithValue("$version", plugin.Version);
        command.Parameters.AddWithValue("$hash", plugin.ContentHash);
        command.Parameters.AddWithValue("$path", plugin.InstallPath);
        command.Parameters.AddWithValue("$state", (int)plugin.State);
        command.Parameters.AddWithValue("$source", plugin.SourcePath);
        command.Parameters.AddWithValue("$at", plugin.InstalledAt.ToString("O"));
        command.Parameters.AddWithValue("$components", components);
        command.Parameters.AddWithValue("$servers", servers);
        command.Parameters.AddWithValue("$overlay", (int)plugin.OverlaySource);
    }

    private static InstalledPlugin Map(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), (InstalledPluginState)reader.GetInt32(5), reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
            InstalledPluginJson.DeserializeComponents(reader.GetString(8)),
            InstalledPluginJson.DeserializeServers(reader.GetString(9)),
            (PluginOverlaySource)reader.GetInt32(10));

    private static bool SameContent(InstalledPlugin stored, InstalledPlugin plugin) =>
        stored.ContentHash.Equals(plugin.ContentHash, StringComparison.Ordinal);

    private static PluginPackageException Conflict(InstalledPlugin plugin) =>
        new(PluginPackageErrorCode.InstallConflict,
            $"a different package already owns '{plugin.Name}' {plugin.Version}");
}
