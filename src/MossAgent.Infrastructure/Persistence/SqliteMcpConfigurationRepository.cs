using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteMcpConfigurationRepository(SqliteConnectionFactory connections)
    : IMcpConfigurationRepository
{
    public async Task<IReadOnlyList<McpServerProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,name,transport,command,arguments_json,working_directory,
                environment_json,url,headers_json,proxy_id,is_enabled
            FROM mcp_servers ORDER BY name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<McpServerProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Read(reader));
        }

        return results;
    }

    public async Task SaveAsync(McpServerProfile profile, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO mcp_servers(id,name,transport,command,arguments_json,working_directory,
                environment_json,url,headers_json,proxy_id,is_enabled)
            VALUES($id,$name,$transport,$command,$arguments,$working,$environment,$url,$headers,$proxy,$enabled)
            ON CONFLICT(id) DO UPDATE SET name=$name,transport=$transport,command=$command,
                arguments_json=$arguments,working_directory=$working,environment_json=$environment,
                url=$url,headers_json=$headers,proxy_id=$proxy,is_enabled=$enabled;
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString());
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$transport", (int)profile.Transport);
        command.Parameters.AddWithValue("$command", (object?)profile.Command ?? DBNull.Value);
        command.Parameters.AddWithValue("$arguments", profile.ArgumentsJson);
        command.Parameters.AddWithValue("$working", (object?)profile.WorkingDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$environment", profile.EnvironmentJson);
        command.Parameters.AddWithValue("$url", (object?)profile.Url?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$headers", profile.HeadersJson);
        command.Parameters.AddWithValue("$proxy", (object?)profile.ProxyId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$enabled", profile.IsEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM mcp_servers WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static McpServerProfile Read(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        Uri? url = reader.IsDBNull(7) ? null : new Uri(reader.GetString(7));
        Guid? proxyId = reader.IsDBNull(9) ? null : Guid.Parse(reader.GetString(9));
        return new McpServerProfile(
            Guid.Parse(reader.GetString(0)), reader.GetString(1),
            (McpTransportKind)reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
            url, reader.GetString(8), proxyId, reader.GetBoolean(10));
    }
}
