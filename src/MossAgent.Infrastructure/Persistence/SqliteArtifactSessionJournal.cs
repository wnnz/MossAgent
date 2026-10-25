using Microsoft.Data.Sqlite;
using MossAgent.Application.Artifacts;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteArtifactSessionJournal(SqliteConnectionFactory connections,
    ITaskArtifactPaths paths, TimeSpan? staleGrace = null) : IArtifactSessionJournal
{
    private const int PendingReadLimit = 4096;
    private readonly TimeSpan _staleGrace = staleGrace ?? TimeSpan.FromMinutes(5);

    public async Task<long> RecordAsync(Guid taskId, string fileName, string fullPath,
        ArtifactSessionKind kind, long? byteCount, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            VALUES($task,$file,$path,$kind,0,$bytes,NULL,$now,$now)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString("N"));
        command.Parameters.AddWithValue("$file", fileName);
        command.Parameters.AddWithValue("$path", fullPath);
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$bytes", (object?)byteCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public Task MarkCompletedAsync(long recordId, long byteCount, CancellationToken token) => UpdateAsync("""
        UPDATE artifact_session_records SET stage=1,byte_count=$bytes,updated_at=$now
        WHERE id=$id AND stage=0;
        """, token, ("$id", recordId), ("$bytes", byteCount));

    public Task MarkRetainedAsync(long recordId, CancellationToken token) => UpdateAsync("""
        UPDATE artifact_session_records SET stage=2,updated_at=$now
        WHERE id=$id AND stage IN (0,1);
        """, token, ("$id", recordId));

    public Task MarkDiscardedAsync(long recordId, CancellationToken token) => UpdateAsync("""
        UPDATE artifact_session_records SET stage=3,error_code=NULL,updated_at=$now
        WHERE id=$id AND stage IN (0,1,4);
        """, token, ("$id", recordId));

    public Task MarkCleanupUnknownAsync(long recordId, string errorCode, CancellationToken token) => UpdateAsync("""
        UPDATE artifact_session_records SET stage=4,error_code=$error,updated_at=$now
        WHERE id=$id AND stage IN (0,1);
        """, token, ("$id", recordId), ("$error", errorCode));

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var stale = (DateTimeOffset.UtcNow - _staleGrace).ToString("O");
        var pending = await WorktreeIntentSqlite.QueryAsync(connections, $"""
            SELECT id,task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at
            FROM artifact_session_records WHERE stage IN (0,1,4) AND updated_at<$stale
            ORDER BY created_at LIMIT {PendingReadLimit};
            """, Read, cancellationToken, ("$stale", stale)).ConfigureAwait(false);
        foreach (var record in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (stage, error) = Classify(record);
            await WorktreeIntentSqlite.UpdateAsync(connections, """
                UPDATE artifact_session_records SET stage=$stage,error_code=$error,updated_at=$now
                WHERE id=$id AND stage IN (0,1,4);
                """, cancellationToken, ("$id", record.Id), ("$stage", stage), ("$error", error)).ConfigureAwait(false);
        }
    }

    // Orphan classification only ever marks journal rows; files are never deleted here.
    // A recorded path that no longer resolves inside its task artifact directory is
    // treated as unverifiable rather than trusted. ExternalSdkTemp paths legitimately
    // live outside the task directory (e.g. browser SDK temp), so only existence is checked.
    private (int Stage, string? Error) Classify(ArtifactSessionRecord record)
    {
        if (record.Kind == ArtifactSessionKind.ExternalSdkTemp)
        {
            return File.Exists(record.FullPath)
                ? ((int)ArtifactSessionStage.Orphaned, "sdk_temp")
                : ((int)ArtifactSessionStage.Discarded, null);
        }
        if (!IsTaskArtifactFile(record))
            return ((int)ArtifactSessionStage.Orphaned, "unverified");
        if (!File.Exists(record.FullPath))
            return ((int)ArtifactSessionStage.Discarded, null);
        return ((int)ArtifactSessionStage.Orphaned, record.Stage switch
        {
            ArtifactSessionStage.Recorded => "staged",
            ArtifactSessionStage.Completed => "completed",
            _ => "cleanup_unknown",
        });
    }

    private bool IsTaskArtifactFile(ArtifactSessionRecord record)
    {
        try
        {
            var directory = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(paths.GetTaskDirectory(record.TaskId)));
            var full = Path.GetFullPath(record.FullPath);
            return Path.GetFileName(full).Equals(record.FileName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetDirectoryName(full), directory, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadUnresolvedAsync(
        ProjectProfile project, CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT id,task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at
            FROM artifact_session_records WHERE stage=5 AND EXISTS(
                SELECT 1 FROM agent_tasks
                WHERE replace(agent_tasks.id,'-','')=artifact_session_records.task_id
                  AND agent_tasks.project_id=$project)
            ORDER BY created_at;
            """, Read, cancellationToken, ("$project", project.Id.ToString()));

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadTaskAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT id,task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at
            FROM artifact_session_records WHERE task_id=$task ORDER BY created_at;
            """, Read, cancellationToken, ("$task", taskId.ToString("N")));

    public async Task AcknowledgeAsync(long recordId, CancellationToken cancellationToken)
    {
        if (await UpdateAsync("""
            UPDATE artifact_session_records SET stage=6,updated_at=$now
            WHERE id=$id AND stage=5;
            """, cancellationToken, ("$id", recordId)) != 1)
            throw new ArtifactSessionStaleException();
    }

    private Task<int> UpdateAsync(string commandText, CancellationToken token,
        params (string Name, object? Value)[] parameters) =>
        WorktreeIntentSqlite.UpdateAsync(connections, commandText, token, parameters);

    private static ArtifactSessionRecord Read(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        Guid.ParseExact(reader.GetString(1), "N"),
        reader.GetString(2),
        reader.GetString(3),
        (ArtifactSessionKind)reader.GetInt32(4),
        (ArtifactSessionStage)reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetInt64(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        DateTimeOffset.Parse(reader.GetString(8)),
        DateTimeOffset.Parse(reader.GetString(9)));
}
