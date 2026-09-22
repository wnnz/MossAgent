using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteToolExecutionAuditSink(SqliteConnectionFactory connections)
    : IToolExecutionAuditSink
{
    public async Task<Guid> StartAsync(
        ToolExecutionAuditStart entry,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tool_executions(
                id,task_id,call_id,tool_name,risk_level,approval_policy,started_at)
            VALUES($id,$task,$call,$tool,$risk,$policy,$started);
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$task", entry.TaskId.ToString());
        command.Parameters.AddWithValue("$call", entry.CallId);
        command.Parameters.AddWithValue("$tool", entry.ToolName);
        command.Parameters.AddWithValue("$risk", (object?)(int?)entry.RiskLevel ?? DBNull.Value);
        command.Parameters.AddWithValue("$policy", (int)entry.ApprovalPolicy);
        command.Parameters.AddWithValue("$started", entry.StartedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task CompleteAsync(
        Guid executionId,
        ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var taskId = await CompleteExecutionAsync(
            connection, transaction, executionId, completion, cancellationToken);
        foreach (var artifact in artifacts)
        {
            await InsertArtifactAsync(
                connection, transaction, executionId, taskId, artifact, completion.CompletedAt,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> CompleteExecutionAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        Guid executionId,
        ToolExecutionAuditCompletion completion,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE tool_executions SET completed_at=$completed,approval_granted=$approved,
                is_success=$success,error_code=$error,output_length=$length
            WHERE id=$id RETURNING task_id;
            """;
        command.Parameters.AddWithValue("$id", executionId.ToString());
        command.Parameters.AddWithValue("$completed", completion.CompletedAt.ToString("O"));
        command.Parameters.AddWithValue("$approved", (object?)completion.ApprovalGranted ?? DBNull.Value);
        command.Parameters.AddWithValue("$success", completion.IsSuccess);
        command.Parameters.AddWithValue("$error", (object?)completion.ErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$length", completion.OutputLength);
        return (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("工具审计记录不存在。");
    }

    private static async Task InsertArtifactAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        Guid executionId,
        string taskId,
        ToolArtifact artifact,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO tool_artifacts(
                id,execution_id,task_id,name,path,media_type,length,sha256,created_at)
            VALUES($id,$execution,$task,$name,$path,$media,$length,$sha,$created);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$execution", executionId.ToString());
        command.Parameters.AddWithValue("$task", taskId);
        command.Parameters.AddWithValue("$name", artifact.Name);
        command.Parameters.AddWithValue("$path", artifact.Path);
        command.Parameters.AddWithValue("$media", artifact.MediaType);
        command.Parameters.AddWithValue("$length", artifact.Length);
        command.Parameters.AddWithValue("$sha", artifact.Sha256);
        command.Parameters.AddWithValue("$created", createdAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
