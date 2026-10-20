using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringBoundary
{
    public static async Task<ConversationSteeringApplication?> ApplyAsync(SqliteConnection connection, ConversationSteeringRun expected,
        Guid[] ids, ConversationMessage? assistant, DateTimeOffset now, CancellationToken token, ConversationSteeringAuthorization? authorization)
    {
        await using var transaction = connection.BeginTransaction();
        await SqliteConversationSteeringAuthorization.ValidateAsync(connection, transaction, expected.TaskId, authorization, token);
        var pending = await SqliteConversationSteeringReader.ReadPendingAsync(connection, transaction, expected, token);
        if (ids.Length > pending.Count || !ids.SequenceEqual(pending.Take(ids.Length).Select(input => input.Id))) return null;
        await using var command = SqliteConversationSteeringReader.Create(connection, transaction, expected.TaskId, expected.Id);
        command.CommandText = $"""
            UPDATE conversation_steering_runs SET revision=revision+1 WHERE run_id=$run AND task_id=$task
              AND revision=$revision AND {SqliteConversationSteeringRuns.ActiveScope};
            """;
        command.Parameters.AddWithValue("$revision", expected.Revision);
        if (await command.ExecuteNonQueryAsync(token) != 1) return null;
        var appendedAt = await NextMessageTimeAsync(connection, transaction, expected.TaskId, now, token);
        assistant = assistant is null ? null : assistant with { CreatedAt = appendedAt };
        if (assistant is not null)
            await SqliteWorkspaceWriter.WriteMessageAsync(connection, transaction, assistant, token);
        var messages = new List<ConversationMessage>();
        foreach (var input in pending.Take(ids.Length))
        {
            var message = new ConversationMessage(input.Id, input.TaskId, MessageRole.User, input.Text, appendedAt);
            await SqliteWorkspaceWriter.WriteMessageAsync(connection, transaction, message, token);
            messages.Add(message);
            command.CommandText = "UPDATE conversation_steering_inputs SET state=1,applied_at=$now WHERE id=$id AND run_id=$run AND state=0;";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", input.Id.ToString());
            command.Parameters.AddWithValue("$run", expected.Id.ToString());
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Steering prefix changed.");
        }
        await transaction.CommitAsync(token);
        return new(expected with { Revision = expected.Revision + 1 },
            pending.Take(ids.Length).Select(input => input with { State = ConversationSteeringInputState.AppliedAtBoundary, AppliedAt = now }).ToArray(),
            messages.ToArray(), assistant);
    }

    private static async Task<DateTimeOffset> NextMessageTimeAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, DateTimeOffset now, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT created_at FROM messages WHERE task_id=$task ORDER BY created_at DESC,rowid DESC LIMIT 1;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        var result = await command.ExecuteScalarAsync(token);
        if (result is string value && DateTimeOffset.Parse(value) >= now) return DateTimeOffset.Parse(value).AddTicks(1);
        return now;
    }
}
