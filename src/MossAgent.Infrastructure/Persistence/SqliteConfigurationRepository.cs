using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConfigurationRepository(SqliteConnectionFactory connections)
    : IConfigurationRepository
{
    public async Task<IReadOnlyList<ProxyProfile>> GetProxiesAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,protocol,host,port,username,password,is_default,is_enabled FROM proxies ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ProxyProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProxyProfile(
                Guid.Parse(reader.GetString(0)), reader.GetString(1),
                (ProxyProtocol)reader.GetInt32(2), reader.GetString(3), reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetBoolean(7), reader.GetBoolean(8)));
        }

        return results;
    }

    public async Task SaveProxyAsync(ProxyProfile proxy, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        if (proxy.IsDefault)
        {
            await ClearDefaultAsync(connection, transaction, "proxies", cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO proxies(id,name,protocol,host,port,username,password,is_default,is_enabled)
            VALUES($id,$name,$protocol,$host,$port,$username,$password,$default,$enabled)
            ON CONFLICT(id) DO UPDATE SET name=$name,protocol=$protocol,host=$host,port=$port,
            username=$username,password=$password,is_default=$default,is_enabled=$enabled;
            """;
        command.Parameters.AddWithValue("$id", proxy.Id.ToString());
        command.Parameters.AddWithValue("$name", proxy.Name);
        command.Parameters.AddWithValue("$protocol", (int)proxy.Protocol);
        command.Parameters.AddWithValue("$host", proxy.Host);
        command.Parameters.AddWithValue("$port", proxy.Port);
        command.Parameters.AddWithValue("$username", (object?)proxy.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("$password", (object?)proxy.Password ?? DBNull.Value);
        command.Parameters.AddWithValue("$default", proxy.IsDefault);
        command.Parameters.AddWithValue("$enabled", proxy.IsEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task DeleteProxyAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync("proxies", id, cancellationToken);

    public async Task<IReadOnlyList<AiProvider>> GetProvidersAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,protocol,base_uri,api_key,proxy_id,is_default,is_enabled,headers_json FROM providers ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AiProvider>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(8)) ?? [];
            results.Add(new AiProvider(
                Guid.Parse(reader.GetString(0)), reader.GetString(1),
                (ProviderProtocol)reader.GetInt32(2), new Uri(reader.GetString(3)), reader.GetString(4),
                reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5)),
                reader.GetBoolean(6), reader.GetBoolean(7), headers));
        }

        return results;
    }

    public async Task SaveProviderAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        if (provider.IsDefault)
        {
            await ClearDefaultAsync(connection, transaction, "providers", cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO providers(id,name,protocol,base_uri,api_key,proxy_id,is_default,is_enabled,headers_json)
            VALUES($id,$name,$protocol,$uri,$key,$proxy,$default,$enabled,$headers)
            ON CONFLICT(id) DO UPDATE SET name=$name,protocol=$protocol,base_uri=$uri,api_key=$key,
            proxy_id=$proxy,is_default=$default,is_enabled=$enabled,headers_json=$headers;
            """;
        command.Parameters.AddWithValue("$id", provider.Id.ToString());
        command.Parameters.AddWithValue("$name", provider.Name);
        command.Parameters.AddWithValue("$protocol", (int)provider.Protocol);
        command.Parameters.AddWithValue("$uri", provider.BaseUri.ToString());
        command.Parameters.AddWithValue("$key", provider.ApiKey);
        command.Parameters.AddWithValue("$proxy", (object?)provider.ProxyId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$default", provider.IsDefault);
        command.Parameters.AddWithValue("$enabled", provider.IsEnabled);
        command.Parameters.AddWithValue("$headers", JsonSerializer.Serialize(provider.Headers));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync("providers", id, cancellationToken);

    public async Task<IReadOnlyList<ModelProfile>> GetModelsAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,provider_id,model_id,display_name,context_length,maximum_output_tokens,
            reasoning_effort,temperature,top_p,supports_images,supports_files,is_default,is_enabled,
            advanced_parameters_json FROM models WHERE provider_id=$provider ORDER BY display_name;
            """;
        command.Parameters.AddWithValue("$provider", providerId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ModelProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadModel(reader));
        }

        return results;
    }

    public async Task SaveModelAsync(ModelProfile model, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        if (model.IsDefault)
        {
            await ClearDefaultModelAsync(
                connection, transaction, model.ProviderId, cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO models(id,provider_id,model_id,display_name,context_length,maximum_output_tokens,
            reasoning_effort,temperature,top_p,supports_images,supports_files,is_default,is_enabled,advanced_parameters_json)
            VALUES($id,$provider,$model,$name,$context,$output,$reasoning,$temperature,$top_p,$images,$files,$default,$enabled,$json)
            ON CONFLICT(provider_id,model_id) DO UPDATE SET display_name=$name,context_length=$context,
            maximum_output_tokens=$output,reasoning_effort=$reasoning,temperature=$temperature,top_p=$top_p,
            supports_images=$images,supports_files=$files,is_default=$default,is_enabled=$enabled,advanced_parameters_json=$json;
            """;
        AddModelParameters(command, model);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task DeleteModelAsync(Guid id, CancellationToken cancellationToken) =>
        DeleteAsync("models", id, cancellationToken);

    private static ModelProfile ReadModel(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
        reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5),
        reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetDouble(7),
        reader.IsDBNull(8) ? null : reader.GetDouble(8), reader.GetBoolean(9),
        reader.GetBoolean(10), reader.GetBoolean(11), reader.GetBoolean(12), reader.GetString(13));

    private static void AddModelParameters(SqliteCommand command, ModelProfile model)
    {
        command.Parameters.AddWithValue("$id", model.Id.ToString());
        command.Parameters.AddWithValue("$provider", model.ProviderId.ToString());
        command.Parameters.AddWithValue("$model", model.ModelId);
        command.Parameters.AddWithValue("$name", model.DisplayName);
        command.Parameters.AddWithValue("$context", model.ContextLength);
        command.Parameters.AddWithValue("$output", model.MaximumOutputTokens);
        command.Parameters.AddWithValue("$reasoning", model.ReasoningEffort);
        command.Parameters.AddWithValue("$temperature", (object?)model.Temperature ?? DBNull.Value);
        command.Parameters.AddWithValue("$top_p", (object?)model.TopP ?? DBNull.Value);
        command.Parameters.AddWithValue("$images", model.SupportsImages);
        command.Parameters.AddWithValue("$files", model.SupportsFiles);
        command.Parameters.AddWithValue("$default", model.IsDefault);
        command.Parameters.AddWithValue("$enabled", model.IsEnabled);
        command.Parameters.AddWithValue("$json", model.AdvancedParametersJson);
    }

    private async Task DeleteAsync(string table, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearDefaultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {table} SET is_default=0 WHERE is_default=1";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearDefaultModelAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE models SET is_default=0 WHERE provider_id=$provider";
        command.Parameters.AddWithValue("$provider", providerId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
