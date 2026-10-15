using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConversationPreferenceRepository(SqliteConnectionFactory connections)
    : IConversationPreferenceRepository
{
    public Task<ConversationModelPreference?> GetAsync(Guid taskId, CancellationToken cancellationToken) =>
        Task.Run(() => ReadCoreAsync(taskId, cancellationToken), cancellationToken);

    private async Task<ConversationModelPreference?> ReadCoreAsync(Guid taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider_id,model_profile_id,reasoning_effort,agent_profile_id,follow_agent_reasoning
            FROM conversation_model_preferences WHERE task_id=$task;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ConversationModelPreference(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                reader.GetBoolean(4))
            : null;
    }

    public Task<bool> SaveAsync(Guid taskId, ConversationModelPreference preference, CancellationToken cancellationToken) =>
        Task.Run(() => SaveCoreAsync(taskId, preference, cancellationToken), cancellationToken);

    private async Task<bool> SaveCoreAsync(Guid taskId, ConversationModelPreference preference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (taskId == Guid.Empty || preference.ProviderId == Guid.Empty || preference.ModelProfileId == Guid.Empty
            || preference.AgentProfileId == Guid.Empty || preference.ReasoningEffort is not
                (null or "none" or "minimal" or "low" or "medium" or "high" or "xhigh" or "max" or "ultra")
            || preference.FollowAgentReasoning && (preference.AgentProfileId is null || preference.ReasoningEffort is not null))
            throw new ArgumentException("Invalid conversation preference.");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO conversation_model_preferences(task_id,provider_id,model_profile_id,reasoning_effort,agent_profile_id,follow_agent_reasoning)
            SELECT task.id,$provider,$model,$reasoning,$agent,$follow FROM agent_tasks task
            WHERE task.id=$task AND NOT EXISTS (
                SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=task.id)
            ON CONFLICT(task_id) DO UPDATE SET provider_id=$provider,model_profile_id=$model,
                reasoning_effort=$reasoning,agent_profile_id=$agent,follow_agent_reasoning=$follow;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$provider", preference.ProviderId.ToString());
        command.Parameters.AddWithValue("$model", preference.ModelProfileId.ToString());
        command.Parameters.AddWithValue("$reasoning", (object?)preference.ReasoningEffort ?? DBNull.Value);
        command.Parameters.AddWithValue("$agent", (object?)preference.AgentProfileId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$follow", preference.FollowAgentReasoning);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }
}
