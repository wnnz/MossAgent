using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSubagentRunJournal(SqliteConnectionFactory connections) : ISubagentRunJournal
{
    private const int TaskReadLimit = 256;

    public async Task<bool> BeginAsync(SubagentRunRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Status != SubagentRunStatus.Running)
            throw new ArgumentException("A run can only begin in the running state.", nameof(record));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // A missing or archived parent task yields zero rows instead of a foreign key error,
        // so callers can distinguish "skipped journal" from a storage failure.
        command.CommandText = """
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            SELECT $run,$task,$call,$profile,$snapshot,$status,$error,$created,$updated
            WHERE EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task
                AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks WHERE task_id=$task));
            """;
        command.Parameters.AddWithValue("$run", record.RunId.ToString("D"));
        command.Parameters.AddWithValue("$task", record.TaskId.ToString("D"));
        command.Parameters.AddWithValue("$call", record.ParentCallId);
        command.Parameters.AddWithValue("$profile", record.ProfileName);
        command.Parameters.AddWithValue("$snapshot", record.SnapshotJson);
        command.Parameters.AddWithValue("$status", ToStorage(record.Status));
        command.Parameters.AddWithValue("$error", (object?)record.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", record.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", record.UpdatedAt.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task CompleteAsync(Guid runId, SubagentRunStatus status, string? errorCode,
        CancellationToken cancellationToken)
    {
        if (status == SubagentRunStatus.Running)
            throw new ArgumentException("A run cannot be completed back into the running state.", nameof(status));
        return WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE subagent_runs SET status=$status,error_code=$error,updated_at=$now
            WHERE run_id=$run AND status='running';
            """, cancellationToken, ("$run", runId.ToString("D")), ("$status", ToStorage(status)),
            ("$error", errorCode));
    }

    public Task<int> MarkInterruptedRunningAsync(CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE subagent_runs SET status='interrupted',updated_at=$now WHERE status='running';
            """, cancellationToken);

    public Task<IReadOnlyList<SubagentRunRecord>> ReadTaskAsync(Guid taskId, CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.QueryAsync(connections, $"""
            SELECT run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at
            FROM subagent_runs WHERE task_id=$task ORDER BY created_at,run_id LIMIT {TaskReadLimit};
            """, Read, cancellationToken, ("$task", taskId.ToString("D")));

    private static SubagentRunRecord Read(SqliteDataReader reader) => new(
        Guid.ParseExact(reader.GetString(0), "D"),
        Guid.ParseExact(reader.GetString(1), "D"),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        FromStorage(reader.GetString(5)),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        DateTimeOffset.Parse(reader.GetString(7)),
        DateTimeOffset.Parse(reader.GetString(8)));

    private static string ToStorage(SubagentRunStatus status) => status switch
    {
        SubagentRunStatus.Running => "running",
        SubagentRunStatus.Completed => "completed",
        SubagentRunStatus.Failed => "failed",
        SubagentRunStatus.Cancelled => "cancelled",
        SubagentRunStatus.Interrupted => "interrupted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown subagent run status.")
    };

    private static SubagentRunStatus FromStorage(string status) => status switch
    {
        "running" => SubagentRunStatus.Running,
        "completed" => SubagentRunStatus.Completed,
        "failed" => SubagentRunStatus.Failed,
        "cancelled" => SubagentRunStatus.Cancelled,
        "interrupted" => SubagentRunStatus.Interrupted,
        _ => throw new InvalidOperationException("Invalid stored subagent run status.")
    };
}
