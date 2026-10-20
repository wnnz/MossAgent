using System.Text;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringAcceptance
{
    public static async Task<ConversationSteeringAcceptance?> AcceptAsync(SqliteConnection connection,
        SqliteConversationDraftWriter drafts, Guid taskId, Guid runId, Guid inputId, string text,
        ConversationDraftUpdate? draft, DateTimeOffset now, CancellationToken token, ConversationSteeringAuthorization? authorization)
    {
        await using var transaction = connection.BeginTransaction();
        await SqliteConversationSteeringAuthorization.ValidateAsync(connection, transaction, taskId, authorization, token);
        var run = await SqliteConversationSteeringReader.ReadRunAsync(connection, transaction, taskId, runId, token);
        if (run is null) return null;
        if (draft is not null && !await MatchesDraftScopeAsync(connection, transaction, taskId, draft, token)) return null;
        var duplicate = await SqliteConversationSteeringReader.ReadInputAsync(connection, transaction, inputId, token);
        if (duplicate is not null)
        {
            if (duplicate.TaskId != taskId || duplicate.RunId != runId || duplicate.Text != text)
                throw new ArgumentException("A steering identity cannot be reused with different input.");
            return new(duplicate, run.Revision, true);
        }
        if (!run.IsOpen || run.Revision == long.MaxValue) return null;
        if (!await InsertAsync(connection, transaction, taskId, runId, inputId, text, now, token)) return null;
        if (draft is not null && !await drafts.WriteAsync(connection, transaction, [draft], token))
            throw new ConversationDraftConflictException();
        await using var advance = SqliteConversationSteeringReader.Create(connection, transaction, taskId, runId);
        advance.CommandText = "UPDATE conversation_steering_runs SET revision=revision+1 WHERE task_id=$task AND run_id=$run;";
        await advance.ExecuteNonQueryAsync(token);
        var input = await SqliteConversationSteeringReader.ReadInputAsync(connection, transaction, inputId, token)
            ?? throw new InvalidOperationException("Missing steering acknowledgement.");
        await transaction.CommitAsync(token);
        return new(input, run.Revision + 1, false);
    }

    private static async Task<bool> MatchesDraftScopeAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, ConversationDraftUpdate draft, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM agent_tasks WHERE id=$task AND project_id IS $project
            AND NOT EXISTS(SELECT 1 FROM conversation_draft_attachments WHERE scope_key=$scope);
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", (object?)draft.Scope.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$scope", draft.Scope.Key);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token)) == 1;
    }

    private static async Task<bool> InsertAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, Guid runId, Guid inputId, string text, DateTimeOffset now, CancellationToken token)
    {
        await using var command = SqliteConversationSteeringReader.Create(connection, transaction, taskId, runId);
        command.CommandText = $"""
            INSERT INTO conversation_steering_inputs(id,task_id,run_id,text,byte_count,state,accepted_at)
            SELECT $id,$task,$run,$text,$bytes,0,$now WHERE {SqliteConversationSteeringRuns.ActiveScope}
              AND (SELECT COUNT(*) FROM conversation_steering_inputs WHERE run_id=$run AND state=0)<$maximum
              AND (SELECT COALESCE(SUM(byte_count),0) FROM conversation_steering_inputs WHERE run_id=$run AND state=0)+$bytes<=$budget
              AND NOT EXISTS(SELECT 1 FROM messages WHERE id=$id)
              AND NOT EXISTS(SELECT 1 FROM conversation_followups WHERE id=$id);
            """;
        command.Parameters.AddWithValue("$id", inputId.ToString());
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$bytes", Encoding.UTF8.GetByteCount(text));
        command.Parameters.AddWithValue("$maximum", ConversationSteeringInput.MaximumPendingItems);
        command.Parameters.AddWithValue("$budget", ConversationSteeringInput.MaximumPendingBytes);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        return await command.ExecuteNonQueryAsync(token) == 1;
    }
}
