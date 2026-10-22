using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringReader
{
    internal const string Columns =
        "id,task_id,run_id,sequence,substr(text,1,32769),state,accepted_at,applied_at,interrupted_at,attachments";

    public static async Task<ConversationSteeringRun?> ReadRunAsync(SqliteConnection connection, SqliteTransaction? transaction,
        Guid taskId, Guid runId, CancellationToken token)
    {
        await using var command = Create(connection, transaction, taskId, runId);
        command.CommandText = "SELECT revision,started_at,closed_at FROM conversation_steering_runs WHERE task_id=$task AND run_id=$run;";
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new(runId, taskId, reader.GetInt64(0), DateTimeOffset.Parse(reader.GetString(1)),
            reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)));
    }

    public static async Task<ConversationSteeringInput?> ReadInputAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid inputId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM conversation_steering_inputs WHERE id=$id;";
        command.Parameters.AddWithValue("$id", inputId.ToString());
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? ReadInput(reader) : null;
    }

    public static ConversationSteeringInput ReadInput(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var taskId = Guid.Parse(reader.GetString(1));
        var attachments = ReadAttachments(reader.GetString(9), taskId, id);
        var input = new ConversationSteeringInput(id, taskId,
            Guid.Parse(reader.GetString(2)), reader.GetInt64(3), reader.GetString(4),
            (ConversationSteeringInputState)reader.GetInt32(5), DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            attachments.Length == 0 ? null : attachments);
        if (input.Text.Length > ConversationSteeringInput.MaximumTextLength || !Enum.IsDefined(input.State)
            || string.IsNullOrWhiteSpace(input.Text)) throw new InvalidDataException("Invalid steering receipt.");
        return input;
    }

    public static async Task<IReadOnlyList<ConversationSteeringInput>> ReadPendingAsync(SqliteConnection connection,
        SqliteTransaction transaction, ConversationSteeringRun expected, CancellationToken token)
    {
        await using var command = Create(connection, transaction, expected.TaskId, expected.Id);
        command.CommandText = $"SELECT {Columns} FROM conversation_steering_inputs "
            + "WHERE run_id=$run AND task_id=$task AND state=0 ORDER BY sequence LIMIT 17;";
        await using var reader = await command.ExecuteReaderAsync(token);
        var inputs = new List<ConversationSteeringInput>();
        while (await reader.ReadAsync(token)) inputs.Add(ReadInput(reader));
        if (inputs.Count > ConversationSteeringInput.MaximumPendingItems
            || inputs.Sum(input => System.Text.Encoding.UTF8.GetByteCount(input.Text)) > ConversationSteeringInput.MaximumPendingBytes)
            throw new InvalidDataException("Steering pending input exceeds its budget.");
        return inputs;
    }

    public static SqliteCommand Create(SqliteConnection connection, SqliteTransaction? transaction, Guid taskId, Guid runId)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$run", runId.ToString());
        return command;
    }

    /// <summary>
    /// Parses and validates the stored attachment snapshot metadata of one steering input.
    /// </summary>
    /// <param name="json">Stored JSON array written by the accept transaction.</param>
    /// <param name="taskId">Task that must own every snapshot.</param>
    /// <param name="inputId">Steering input id that must own every snapshot.</param>
    private static ConversationAttachment[] ReadAttachments(string json, Guid taskId, Guid inputId)
    {
        ConversationAttachment[]? items;
        try { items = JsonSerializer.Deserialize<ConversationAttachment[]>(json); }
        catch (JsonException) { throw new InvalidDataException("Invalid steering attachment snapshot."); }
        if (items is null || items.Length == 0) return [];
        ConversationAttachmentValidation.Validate(taskId, inputId, items);
        return items;
    }
}
