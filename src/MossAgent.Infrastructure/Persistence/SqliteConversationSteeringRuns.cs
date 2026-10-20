using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringRuns
{
    internal const string ActiveScope = """
        EXISTS(SELECT 1 FROM conversation_steering_runs run JOIN agent_tasks task ON task.id=run.task_id
          WHERE run.task_id=$task AND run.run_id=$run AND run.closed_at IS NULL
          AND task.status IN (1,2) AND task.project_id IS run.project_id
          AND task.worktree_path IS run.worktree_path AND task.approval_policy=run.approval_policy
          AND task.created_at=run.task_created_at
          AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks WHERE task_id=$task)
          AND NOT EXISTS(SELECT 1 FROM source_review_sessions WHERE task_id=$task))
        """;

    public static async Task<ConversationSteeringRun?> OpenAsync(SqliteConnection connection, AgentTask expected,
        Guid runId, DateTimeOffset now, CancellationToken token, ConversationSteeringAuthorization? authorization)
    {
        if (expected.Status is not (AgentTaskStatus.Running or AgentTaskStatus.WaitingForApproval)) return null;
        await using var transaction = connection.BeginTransaction();
        await SqliteConversationSteeringAuthorization.ValidateAsync(connection, transaction, expected.Id, authorization, token);
        await using var command = SqliteConversationSteeringReader.Create(connection, transaction, expected.Id, runId);
        command.CommandText = """
            INSERT INTO conversation_steering_runs(run_id,task_id,revision,project_id,worktree_path,approval_policy,
              task_created_at,started_at)
            SELECT $run,$task,0,$project,$worktree,$policy,$created,$now FROM agent_tasks
            WHERE id=$task AND project_id IS $project AND worktree_path IS $worktree AND approval_policy=$policy
              AND created_at=$created AND updated_at=$updated AND status=$status
              AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks WHERE task_id=$task)
              AND NOT EXISTS(SELECT 1 FROM source_review_sessions WHERE task_id=$task)
            ON CONFLICT DO NOTHING;
            """;
        command.Parameters.AddWithValue("$project", (object?)expected.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$worktree", (object?)expected.WorktreePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$policy", (int)expected.ApprovalPolicy);
        command.Parameters.AddWithValue("$created", expected.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", expected.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)expected.Status);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        if (await command.ExecuteNonQueryAsync(token) != 1) return null;
        await transaction.CommitAsync(token);
        return new(runId, expected.Id, 0, now);
    }

    public static async Task<bool> CloseAsync(SqliteConnection connection, ConversationSteeringRun expected,
        bool interruptPending, DateTimeOffset now, CancellationToken token)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = SqliteConversationSteeringReader.Create(connection, transaction, expected.TaskId, expected.Id);
        command.CommandText = """
            UPDATE conversation_steering_runs SET closed_at=$now,revision=revision+1
            WHERE task_id=$task AND run_id=$run AND closed_at IS NULL AND revision=$revision
            AND ($interrupt=1 OR NOT EXISTS(SELECT 1 FROM conversation_steering_inputs WHERE run_id=$run AND state=0));
            """;
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.Parameters.AddWithValue("$revision", expected.Revision);
        command.Parameters.AddWithValue("$interrupt", interruptPending ? 1 : 0);
        if (await command.ExecuteNonQueryAsync(token) != 1) return false;
        command.CommandText = "UPDATE conversation_steering_inputs SET state=2,interrupted_at=$now WHERE run_id=$run AND state=0;";
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }
}
