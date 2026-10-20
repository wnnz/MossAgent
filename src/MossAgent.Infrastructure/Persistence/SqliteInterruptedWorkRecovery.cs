using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteInterruptedWorkRecovery
{
    public static async Task RecoverAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE agent_tasks
            SET status=$interrupted,updated_at=$updated
            WHERE status IN ($running,$waiting);
            INSERT INTO tool_execution_interruptions(execution_id,interrupted_at)
            SELECT id,$updated FROM tool_executions WHERE completed_at IS NULL
            ON CONFLICT(execution_id) DO NOTHING;
            UPDATE conversation_steering_runs SET closed_at=$updated,
              revision=CASE WHEN revision<9223372036854775807 THEN revision+1 ELSE revision END WHERE closed_at IS NULL;
            UPDATE conversation_steering_inputs SET state=2,interrupted_at=$updated WHERE state=0;
            """;
        command.Parameters.AddWithValue("$interrupted", (int)AgentTaskStatus.Interrupted);
        command.Parameters.AddWithValue("$running", (int)AgentTaskStatus.Running);
        command.Parameters.AddWithValue("$waiting", (int)AgentTaskStatus.WaitingForApproval);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
