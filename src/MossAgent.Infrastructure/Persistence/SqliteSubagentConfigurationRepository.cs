using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSubagentConfigurationRepository(SqliteConnectionFactory connections)
    : ISubagentConfigurationRepository
{
    public async Task<IReadOnlyList<SubagentProfile>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,name,trigger_condition,system_prompt,provider_id,model_profile_id,
                reasoning_effort,share_context,is_enabled
            FROM subagents ORDER BY name COLLATE NOCASE;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var profiles = new List<SubagentProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    public async Task SaveAsync(
        SubagentProfile profile,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO subagents(id,name,trigger_condition,system_prompt,provider_id,
                model_profile_id,reasoning_effort,share_context,is_enabled)
            VALUES($id,$name,$trigger,$prompt,$provider,$model,$reasoning,$share,$enabled)
            ON CONFLICT(id) DO UPDATE SET name=$name,trigger_condition=$trigger,
                system_prompt=$prompt,provider_id=$provider,model_profile_id=$model,
                reasoning_effort=$reasoning,share_context=$share,is_enabled=$enabled;
            """;
        AddProfileParameters(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM subagents WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SubagentProfile ReadProfile(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
        reader.GetString(3), Guid.Parse(reader.GetString(4)), Guid.Parse(reader.GetString(5)),
        reader.GetString(6), reader.GetBoolean(7), reader.GetBoolean(8));

    private static void AddProfileParameters(SqliteCommand command, SubagentProfile profile)
    {
        command.Parameters.AddWithValue("$id", profile.Id.ToString());
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$trigger", profile.TriggerCondition);
        command.Parameters.AddWithValue("$prompt", profile.SystemPrompt);
        command.Parameters.AddWithValue("$provider", profile.ProviderId.ToString());
        command.Parameters.AddWithValue("$model", profile.ModelProfileId.ToString());
        command.Parameters.AddWithValue("$reasoning", profile.ReasoningEffort);
        command.Parameters.AddWithValue("$share", profile.ShareContext);
        command.Parameters.AddWithValue("$enabled", profile.IsEnabled);
    }
}
