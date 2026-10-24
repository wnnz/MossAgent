using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorktreeCreationIntents(SqliteConnectionFactory connections, TimeSpan? staleGrace = null)
{
    private readonly TimeSpan _staleGrace = staleGrace ?? TimeSpan.FromMinutes(5);

    public async Task RecordAsync(Guid taskId, ProjectProfile project, string repository,
        string? baseReference, string baseCommit, string target, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO worktree_creation_intents(
                task_id,project_id,repository,base_reference,base_commit,target,state,stage,created_at,updated_at)
            SELECT $task,$project,$repository,$reference,$commit,$target,0,0,$now,$now
            WHERE EXISTS(SELECT 1 FROM projects WHERE id=$project AND authorization_revision=$revision)
              AND NOT EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task);
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$reference", (object?)baseReference ?? DBNull.Value);
        command.Parameters.AddWithValue("$commit", baseCommit);
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        try
        {
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19) { throw new WorktreeAuthorizationException(); }
        await transaction.CommitAsync(token);
    }

    public async Task RecordIdentityAsync(Guid taskId, string target, DirectoryObjectIdentity identity, CancellationToken token)
    {
        if (await UpdateAsync("""
            UPDATE worktree_creation_intents SET volume_id=$volume,file_id=$file,
                stage=1,updated_at=$now
            WHERE task_id=$task AND state=0 AND stage=0 AND target=$target;
            """, token,
            ("$task", taskId.ToString()), ("$target", target),
            ("$volume", identity.VolumeId), ("$file", identity.FileId)) != 1)
            throw new WorktreeAuthorizationException();
    }

    public async Task MarkStageAsync(Guid taskId, WorktreeCreationIntentStage from,
        WorktreeCreationIntentStage stage, CancellationToken token)
    {
        if (await UpdateAsync("""
            UPDATE worktree_creation_intents SET stage=$stage,updated_at=$now
            WHERE task_id=$task AND stage=$from;
            """, token, ("$task", taskId.ToString()), ("$from", (int)from), ("$stage", (int)stage)) != 1)
            throw new WorktreeAuthorizationException();
    }

    public Task MarkFailedAsync(Guid taskId, string errorCode, CancellationToken token) => UpdateAsync("""
        UPDATE worktree_creation_intents SET stage=3,error_code=$error,updated_at=$now
        WHERE task_id=$task AND stage<3;
        """, token, ("$task", taskId.ToString()), ("$error", errorCode));

    public async Task ReconcileAsync(CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE worktree_creation_intents SET stage=4,updated_at=$now
            WHERE stage<3 AND EXISTS(SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_creation_intents.task_id);
            UPDATE worktree_creation_intents SET stage=5,updated_at=$now
            WHERE stage<3 AND updated_at<$stale
              AND NOT EXISTS(SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_creation_intents.task_id);
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        // 宽限期：并发实例的在途创建更新 updated_at；陈旧行才可能来自崩溃。 lexicographic over "O" timestamps.
        command.Parameters.AddWithValue("$stale", (DateTimeOffset.UtcNow - _staleGrace).ToString("O"));
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    public Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(Guid projectId, CancellationToken token) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT task_id,project_id,repository,base_reference,base_commit,target,stage,error_code,created_at,updated_at
            FROM worktree_creation_intents WHERE project_id=$project AND stage IN (3,5) ORDER BY created_at;
            """, ReadIntent, token, ("$project", projectId.ToString()));

    private static WorktreeCreationIntent ReadIntent(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5),
        (WorktreeCreationIntentStage)reader.GetInt32(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        DateTimeOffset.Parse(reader.GetString(8)),
        reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)));

    public Task<IReadOnlyList<WorktreeCreationIntent>> ReadUnannotatedOrphansAsync(CancellationToken token) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT task_id,project_id,repository,base_reference,base_commit,target,stage,error_code,created_at,updated_at
            FROM worktree_creation_intents WHERE stage=5 AND error_code IS NULL;
            """, ReadIntent, token);

    public Task AnnotateOrphanAsync(Guid taskId, string errorCode, CancellationToken token) => UpdateAsync("""
        UPDATE worktree_creation_intents SET error_code=$error,updated_at=$now
        WHERE task_id=$task AND stage=5 AND error_code IS NULL;
        """, token, ("$task", taskId.ToString()), ("$error", errorCode));

    public async Task AcknowledgeAsync(Guid taskId, CancellationToken token)
    {
        if (await UpdateAsync("""
            UPDATE worktree_creation_intents SET stage=6,updated_at=$now
            WHERE task_id=$task AND stage IN (3,5);
            """, token, ("$task", taskId.ToString())) != 1)
            throw new WorktreeAuthorizationException();
    }

    private Task<int> UpdateAsync(string commandText, CancellationToken token,
        params (string Name, object? Value)[] parameters) =>
        WorktreeIntentSqlite.UpdateAsync(connections, commandText, token, parameters);
}
