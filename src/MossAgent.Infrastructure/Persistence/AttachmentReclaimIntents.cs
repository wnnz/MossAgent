using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

internal static class AttachmentReclaimIntents
{
    private const int RecordedReadLimit = 4096;

    public static async Task RecordAsync(SqliteConnection connection, string path, long byteCount,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO attachment_reclaim_intents(path,byte_count,stage,error_code,created_at,updated_at)
            VALUES($path,$bytes,0,NULL,$now,$now)
            ON CONFLICT(path) DO UPDATE SET stage=0,error_code=NULL,
                byte_count=excluded.byte_count,updated_at=excluded.updated_at
            WHERE attachment_reclaim_intents.stage<>0;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$bytes", byteCount);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task MarkAsync(SqliteConnection connection, string path, AttachmentReclaimStage stage,
        string? errorCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE attachment_reclaim_intents SET stage=$stage,error_code=$error,updated_at=$now
            WHERE path=$path;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$stage", (int)stage);
        command.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task<IReadOnlyList<string>> ReadRecordedAsync(SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT path FROM attachment_reclaim_intents WHERE stage=0 LIMIT {RecordedReadLimit};";
        var paths = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) paths.Add(reader.GetString(0));
        return paths;
    }

    public static async Task PruneAsync(SqliteConnection connection, DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM attachment_reclaim_intents WHERE stage<>0 AND updated_at<$cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
