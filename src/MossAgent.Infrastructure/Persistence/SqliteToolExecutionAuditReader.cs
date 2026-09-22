using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteToolExecutionAuditReader(SqliteConnectionFactory connections)
    : IToolExecutionAuditReader
{
    public async Task<IReadOnlyList<ToolExecutionAuditRecord>> GetRecentAsync(
        Guid taskId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id,e.task_id,e.call_id,e.tool_name,e.risk_level,e.approval_policy,
                e.started_at,e.completed_at,e.approval_granted,e.is_success,e.error_code,
                e.output_length,COUNT(a.id)
            FROM tool_executions e
            LEFT JOIN tool_artifacts a ON a.execution_id=e.id
            WHERE e.task_id=$task
            GROUP BY e.id
            ORDER BY e.started_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<ToolExecutionAuditRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadExecution(reader));
        }

        return records;
    }

    public async Task<IReadOnlyList<ToolArtifactRecord>> GetArtifactsAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,execution_id,task_id,name,path,media_type,length,sha256,created_at
            FROM tool_artifacts WHERE execution_id=$execution ORDER BY created_at;
            """;
        command.Parameters.AddWithValue("$execution", executionId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<ToolArtifactRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ToolArtifactRecord(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetInt64(6), reader.GetString(7),
                DateTimeOffset.Parse(reader.GetString(8))));
        }

        return records;
    }

    private static ToolExecutionAuditRecord ReadExecution(
        Microsoft.Data.Sqlite.SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2),
        reader.GetString(3), reader.IsDBNull(4) ? null : (ToolRiskLevel)reader.GetInt32(4),
        (ApprovalPolicy)reader.GetInt32(5), DateTimeOffset.Parse(reader.GetString(6)),
        reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
        reader.IsDBNull(8) ? null : reader.GetBoolean(8),
        reader.IsDBNull(9) ? null : reader.GetBoolean(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetInt32(11), reader.GetInt32(12));
}
