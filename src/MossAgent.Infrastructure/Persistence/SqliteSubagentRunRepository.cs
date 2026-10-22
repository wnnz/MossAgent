using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSubagentRunRepository(
    SqliteConnectionFactory connections,
    TimeProvider timeProvider) : ISubagentRunRepository
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    public async Task<SubagentRun> StartAsync(SubagentRun run, CancellationToken cancellationToken)
    {
        Validate(run);
        run = run with { OwnerInstanceId = InstanceId };
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO subagent_runs(
                id,task_id,parent_run_id,parent_call_id,profile_id,agent_name,trigger_condition,system_prompt,
                provider_id,provider_name,provider_protocol,model_profile_id,model_id,context_length,
                maximum_output_tokens,reasoning_effort,share_context,owner_instance_id,status,summary,error_code,
                created_at,updated_at,completed_at)
            VALUES($id,$task,$parent,$call,$profile,$agent,$trigger,$prompt,$provider,$providerName,
                $protocol,$modelProfile,$model,$context,$output,$reasoning,$share,$owner,0,$summary,$error,
                $created,$updated,$completed);
            """;
        AddIdentity(command, run);
        command.Parameters.AddWithValue("$summary", (object?)run.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)run.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", run.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", run.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$completed", (object?)run.CompletedAt?.ToString("O") ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return run with { Status = SubagentRunStatus.Running, CompletedAt = null };
    }

    public Task<SubagentRun> CompleteAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Completed, summary, null, cancellationToken);

    public Task<SubagentRun> FailAsync(Guid taskId, Guid runId, string summary, string errorCode,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Failed, summary, errorCode, cancellationToken);

    public Task<SubagentRun> CancelAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken) =>
        FinishAsync(taskId, runId, SubagentRunStatus.Cancelled, summary, "tool_cancelled", cancellationToken);

    public async Task<SubagentRun> MarkInterruptedAsync(Guid taskId, Guid runId, string summary,
        CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty || runId == Guid.Empty)
            throw new ArgumentException("Invalid subagent run identity.");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE subagent_runs
            SET status=4,summary=$summary,error_code='subagent_run_interrupted',
                updated_at=$now,completed_at=$now
            WHERE id=$id AND task_id=$task AND status=0 AND owner_instance_id<>$instance;
            """;
        update.Parameters.AddWithValue("$summary", Bounded(summary, 4096));
        update.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().ToString("O"));
        update.Parameters.AddWithValue("$id", runId.ToString());
        update.Parameters.AddWithValue("$task", taskId.ToString());
        update.Parameters.AddWithValue("$instance", InstanceId);
        var changed = await update.ExecuteNonQueryAsync(cancellationToken);
        var restored = await ReadAsync(connection, taskId, runId, cancellationToken);
        if (changed == 1 || restored.Status == SubagentRunStatus.Interrupted
            && !restored.OwnerInstanceId.Equals(InstanceId, StringComparison.Ordinal))
            return restored;
        throw new InvalidOperationException("The subagent run is still owned by this application instance.");
    }

    public async Task<IReadOnlyList<SubagentRun>> GetRecentAsync(Guid taskId, int limit,
        CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM subagent_runs
            WHERE task_id=$task
            ORDER BY created_at DESC,id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var runs = new List<SubagentRun>();
        while (await reader.ReadAsync(cancellationToken)) runs.Add(Read(reader));
        return runs;
    }

    private async Task<SubagentRun> FinishAsync(Guid taskId, Guid runId, SubagentRunStatus status,
        string summary, string? errorCode, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty || runId == Guid.Empty)
            throw new ArgumentException("Invalid subagent run identity.");
        var now = timeProvider.GetUtcNow();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE subagent_runs
            SET status=$status,summary=$summary,error_code=$error,updated_at=$now,completed_at=$now
            WHERE id=$id AND task_id=$task AND status=0;
            """;
        update.Parameters.AddWithValue("$status", (int)status);
        update.Parameters.AddWithValue("$summary", Bounded(summary, 4096));
        update.Parameters.AddWithValue("$error", errorCode is null ? DBNull.Value : Bounded(errorCode, 64));
        update.Parameters.AddWithValue("$now", now.ToString("O"));
        update.Parameters.AddWithValue("$id", runId.ToString());
        update.Parameters.AddWithValue("$task", taskId.ToString());
        await update.ExecuteNonQueryAsync(cancellationToken);
        return await ReadAsync(connection, taskId, runId, cancellationToken);
    }

    private static async Task<SubagentRun> ReadAsync(SqliteConnection connection, Guid taskId,
        Guid runId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM subagent_runs WHERE id=$id AND task_id=$task;";
        command.Parameters.AddWithValue("$id", runId.ToString());
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Subagent run no longer exists.");
        return Read(reader);
    }

    private static void AddIdentity(SqliteCommand command, SubagentRun run)
    {
        command.Parameters.AddWithValue("$id", run.Id.ToString());
        command.Parameters.AddWithValue("$task", run.TaskId.ToString());
        command.Parameters.AddWithValue("$parent", run.ParentRunId.ToString());
        command.Parameters.AddWithValue("$call", run.ParentCallId);
        command.Parameters.AddWithValue("$profile", run.ProfileId.ToString());
        command.Parameters.AddWithValue("$agent", run.AgentName);
        command.Parameters.AddWithValue("$trigger", run.TriggerCondition);
        command.Parameters.AddWithValue("$prompt", run.SystemPrompt);
        command.Parameters.AddWithValue("$provider", run.ProviderId.ToString());
        command.Parameters.AddWithValue("$providerName", run.ProviderName);
        command.Parameters.AddWithValue("$protocol", (int)run.ProviderProtocol);
        command.Parameters.AddWithValue("$modelProfile", run.ModelProfileId.ToString());
        command.Parameters.AddWithValue("$model", run.ModelId);
        command.Parameters.AddWithValue("$context", run.ContextLength);
        command.Parameters.AddWithValue("$output", run.MaximumOutputTokens);
        command.Parameters.AddWithValue("$reasoning", run.ReasoningEffort);
        command.Parameters.AddWithValue("$share", run.ShareContext ? 1 : 0);
        command.Parameters.AddWithValue("$owner", run.OwnerInstanceId);
    }

    private static void Validate(SubagentRun run)
    {
        if (run.Id == Guid.Empty || run.TaskId == Guid.Empty || run.ParentRunId == Guid.Empty
            || run.ProfileId == Guid.Empty || run.ProviderId == Guid.Empty || run.ModelProfileId == Guid.Empty
            || string.IsNullOrWhiteSpace(run.ParentCallId) || string.IsNullOrWhiteSpace(run.AgentName)
            || string.IsNullOrWhiteSpace(run.TriggerCondition) || string.IsNullOrWhiteSpace(run.SystemPrompt)
            || string.IsNullOrWhiteSpace(run.ProviderName) || string.IsNullOrWhiteSpace(run.ModelId)
            || string.IsNullOrWhiteSpace(run.ReasoningEffort) || string.IsNullOrWhiteSpace(run.OwnerInstanceId)
            || run.ContextLength < 1 || run.MaximumOutputTokens < 1
            || run.ParentCallId.Length > 256 || run.AgentName.Length > 128
            || run.TriggerCondition.Length > 4096 || run.SystemPrompt.Length > 65536
            || run.ProviderName.Length > 256 || run.ModelId.Length > 256
            || run.ReasoningEffort.Length > 128 || run.OwnerInstanceId.Length > 64
            || run.Status != SubagentRunStatus.Running)
            throw new ArgumentException("Invalid subagent run snapshot.");
    }

    private static SubagentRun Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
        Guid.Parse(reader.GetString(2)), reader.GetString(3), Guid.Parse(reader.GetString(4)),
        reader.GetString(5), reader.GetString(6), reader.GetString(7),
        Guid.Parse(reader.GetString(8)), reader.GetString(9),
        (ProviderProtocol)reader.GetInt32(10), Guid.Parse(reader.GetString(11)),
        reader.GetString(12), reader.GetInt32(13), reader.GetInt32(14), reader.GetString(15),
        reader.GetInt32(16) != 0, reader.GetString(17), (SubagentRunStatus)reader.GetInt32(18),
        reader.IsDBNull(19) ? null : reader.GetString(19),
        reader.IsDBNull(20) ? null : reader.GetString(20),
        DateTimeOffset.Parse(reader.GetString(21)), DateTimeOffset.Parse(reader.GetString(22)),
        reader.IsDBNull(23) ? null : DateTimeOffset.Parse(reader.GetString(23)));

    private static string Bounded(string value, int maximum)
    {
        if (value.Length <= maximum) return value;
        var length = maximum;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            length--;
        return value[..length];
    }

    private const string Columns =
        "id,task_id,parent_run_id,parent_call_id,profile_id,agent_name,trigger_condition,system_prompt,"
        + "provider_id,provider_name,provider_protocol,model_profile_id,model_id,context_length,"
        + "maximum_output_tokens,reasoning_effort,share_context,owner_instance_id,status,summary,error_code,"
        + "created_at,updated_at,completed_at";
}
