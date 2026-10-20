using Microsoft.Data.Sqlite;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSourceAccountRepository(SqliteConnectionFactory connections) : ISourceAccountRepository
{
    private const string Columns = "id,name,api_endpoint,access_token,proxy_id,is_enabled,revision";

    public async Task<IReadOnlyList<SourceAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM source_accounts ORDER BY name,id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var accounts = new List<SourceAccount>();
        while (await reader.ReadAsync(cancellationToken)) accounts.Add(Read(reader));
        return accounts;
    }

    public async Task<SourceAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM source_accounts WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<SourceAccount> SaveAsync(SourceAccount account, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await ValidateProxyAsync(connection, transaction, account.ProxyId, account.IsEnabled, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO source_accounts(id,name,api_endpoint,access_token,proxy_id,is_enabled,revision)
            SELECT $id,$name,$endpoint,$token,$proxy,$enabled,1 WHERE $revision=0
            ON CONFLICT(id) DO NOTHING;
            """;
        AddParameters(command, account);
        if (account.Revision > 0)
        {
            command.CommandText = """
                UPDATE source_accounts SET name=$name,api_endpoint=$endpoint,access_token=$token,
                    proxy_id=$proxy,is_enabled=$enabled,revision=revision+1
                WHERE id=$id AND revision=$revision AND revision<9223372036854775807;
                """;
        }
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new SourceConnectionException(SourceConnectionFailure.AccountChanged);
        await transaction.CommitAsync(cancellationToken);
        return account.WithRevision(checked(account.Revision + 1));
    }

    public async Task DeleteAsync(Guid id, long revision, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM source_accounts WHERE id=$id AND revision=$revision;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$revision", revision);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new SourceConnectionException(SourceConnectionFailure.AccountChanged);
    }

    private static SourceAccount Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), reader.GetString(1), new Uri(reader.GetString(2)),
        reader.GetString(3), reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
        reader.GetBoolean(5), reader.GetInt64(6));

    private static void AddParameters(SqliteCommand command, SourceAccount account)
    {
        command.Parameters.AddWithValue("$id", account.Id.ToString());
        command.Parameters.AddWithValue("$name", account.Name);
        command.Parameters.AddWithValue("$endpoint", account.ApiEndpoint.AbsoluteUri);
        command.Parameters.AddWithValue("$token", account.AccessToken);
        command.Parameters.AddWithValue("$proxy", (object?)account.ProxyId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$enabled", account.IsEnabled);
        command.Parameters.AddWithValue("$revision", account.Revision);
    }

    private static async Task ValidateProxyAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid? proxyId, bool requireEnabled, CancellationToken cancellationToken)
    {
        if (proxyId is null) return;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM proxies WHERE id=$id AND ($enabled=0 OR is_enabled=1);";
        command.Parameters.AddWithValue("$id", proxyId.Value.ToString());
        command.Parameters.AddWithValue("$enabled", requireEnabled);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new SourceConnectionException(SourceConnectionFailure.ProxyUnavailable);
    }
}
