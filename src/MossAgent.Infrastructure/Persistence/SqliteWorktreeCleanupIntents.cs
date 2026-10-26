using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorktreeCleanupIntents(SqliteConnectionFactory connections, TimeSpan? staleGrace = null)
{
    private readonly TimeSpan _staleGrace = staleGrace ?? TimeSpan.FromMinutes(5);

    public async Task RecordAsync(Guid taskId, ProjectProfile project, string repository,
        string target, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO worktree_cleanup_intents(
                task_id,project_id,repository,target,stage,error_code,created_at,updated_at)
            SELECT $task,$project,$repository,$target,0,NULL,$now,$now
            WHERE EXISTS(SELECT 1 FROM projects WHERE id=$project AND authorization_revision=$revision)
              AND NOT EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task AND worktree_path IS NULL)
              AND NOT EXISTS(SELECT 1 FROM worktree_cleanup_intents WHERE task_id=$task AND stage<3)
            ON CONFLICT(task_id) DO UPDATE SET stage=0,error_code=NULL,
                repository=excluded.repository,target=excluded.target,
                created_at=excluded.created_at,updated_at=excluded.updated_at
            WHERE worktree_cleanup_intents.stage IN (3,4);
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$repository", repository);
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

    public async Task MarkStageAsync(Guid taskId, WorktreeCleanupIntentStage from,
        WorktreeCleanupIntentStage stage, CancellationToken token)
    {
        if (await UpdateAsync("""
            UPDATE worktree_cleanup_intents SET stage=$stage,updated_at=$now
            WHERE task_id=$task AND stage=$from;
            """, token, ("$task", taskId.ToString()), ("$from", (int)from), ("$stage", (int)stage)) != 1)
            throw new WorktreeAuthorizationException();
    }

    public Task MarkFailedAsync(Guid taskId, string errorCode, CancellationToken token) => UpdateAsync("""
        UPDATE worktree_cleanup_intents SET stage=3,error_code=$error,updated_at=$now
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
            UPDATE worktree_cleanup_intents SET stage=2,updated_at=$now
            WHERE stage<3 AND EXISTS(
                SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_cleanup_intents.task_id
                  AND agent_tasks.worktree_path IS NULL);
            UPDATE worktree_cleanup_intents SET stage=2,updated_at=$now
            WHERE stage=1 AND updated_at<$stale
              AND NOT EXISTS(
                SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_cleanup_intents.task_id);
            UPDATE worktree_cleanup_intents SET stage=4,updated_at=$now
            WHERE stage<3 AND updated_at<$stale;
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        // Grace period: a live cleanup in another instance keeps updating updated_at; only
        // stale rows can come from a crash. Ordering is lexicographic over "O" timestamps.
        command.Parameters.AddWithValue("$stale", (DateTimeOffset.UtcNow - _staleGrace).ToString("O"));
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    // A live intent closes the drain window: in-flight (0,1) and recovery-pending (3,4)
    // stages deny any new run admission on the worktree until the intent reaches a
    // terminal stage (Detached=2 or Acknowledged=5).
    public Task<bool> HasLiveIntentAsync(Guid taskId, CancellationToken token) =>
        WorktreeIntentSqlite.QueryScalarAsync<bool>(connections, """
            SELECT EXISTS(SELECT 1 FROM worktree_cleanup_intents WHERE task_id=$task AND stage IN (0,1,3,4));
            """, token, ("$task", taskId.ToString()));

    public Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(Guid projectId, CancellationToken token) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT task_id,project_id,repository,target,stage,error_code,created_at,updated_at
            FROM worktree_cleanup_intents WHERE project_id=$project AND stage IN (3,4) ORDER BY created_at;
            """, ReadIntent, token, ("$project", projectId.ToString()));

    private static WorktreeCleanupIntent ReadIntent(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        reader.GetString(2),
        reader.GetString(3),
        (WorktreeCleanupIntentStage)reader.GetInt32(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        DateTimeOffset.Parse(reader.GetString(6)),
        reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)));

    public Task<IReadOnlyList<WorktreeCleanupIntent>> ReadUnannotatedOrphansAsync(CancellationToken token) =>
        WorktreeIntentSqlite.QueryAsync(connections, """
            SELECT task_id,project_id,repository,target,stage,error_code,created_at,updated_at
            FROM worktree_cleanup_intents WHERE stage=4 AND error_code IS NULL;
            """, ReadIntent, token);

    public Task AnnotateOrphanAsync(Guid taskId, string errorCode, CancellationToken token) => UpdateAsync("""
        UPDATE worktree_cleanup_intents SET error_code=$error,updated_at=$now
        WHERE task_id=$task AND stage=4 AND error_code IS NULL;
        """, token, ("$task", taskId.ToString()), ("$error", errorCode));

    public async Task AcknowledgeAsync(Guid taskId, CancellationToken token)
    {
        if (await UpdateAsync("""
            UPDATE worktree_cleanup_intents SET stage=5,updated_at=$now
            WHERE task_id=$task AND stage IN (3,4);
            """, token, ("$task", taskId.ToString())) != 1)
            throw new WorktreeAuthorizationException();
    }

    private Task<int> UpdateAsync(string commandText, CancellationToken token,
        params (string Name, object? Value)[] parameters) =>
        WorktreeIntentSqlite.UpdateAsync(connections, commandText, token, parameters);
}
