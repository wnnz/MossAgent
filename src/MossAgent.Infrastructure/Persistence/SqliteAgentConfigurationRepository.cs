using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteAgentConfigurationRepository(SqliteConnectionFactory connections)
    : IAgentConfigurationRepository
{
    public async Task<IReadOnlyList<AgentProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,name,system_prompt,provider_id,model_profile_id,reasoning_effort,is_enabled
            FROM agent_profiles ORDER BY name COLLATE NOCASE;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var profiles = new List<AgentProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(new AgentProfile(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                Guid.Parse(reader.GetString(3)), Guid.Parse(reader.GetString(4)),
                reader.GetString(5), reader.GetBoolean(6)));
        }
        return profiles;
    }

    public async Task SaveAsync(AgentProfile profile, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO agent_profiles(id,name,system_prompt,provider_id,model_profile_id,
                reasoning_effort,is_enabled)
            VALUES($id,$name,$prompt,$provider,$model,$reasoning,$enabled)
            ON CONFLICT(id) DO UPDATE SET name=$name,system_prompt=$prompt,
                provider_id=$provider,model_profile_id=$model,reasoning_effort=$reasoning,
                is_enabled=$enabled;
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString());
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$prompt", profile.SystemPrompt);
        command.Parameters.AddWithValue("$provider", profile.ProviderId.ToString());
        command.Parameters.AddWithValue("$model", profile.ModelProfileId.ToString());
        command.Parameters.AddWithValue("$reasoning", profile.ReasoningEffort);
        command.Parameters.AddWithValue("$enabled", profile.IsEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM agent_profiles WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
