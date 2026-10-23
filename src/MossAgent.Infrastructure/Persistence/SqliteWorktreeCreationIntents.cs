using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorktreeCreationIntents(SqliteConnectionFactory connections)
{
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
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents SET volume_id=$volume,file_id=$file,
                stage=1,updated_at=$now
            WHERE task_id=$task AND state=0 AND stage=0 AND target=$target;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$volume", identity.VolumeId);
        command.Parameters.AddWithValue("$file", identity.FileId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
    }

    public async Task MarkStageAsync(Guid taskId, WorktreeCreationIntentStage from,
        WorktreeCreationIntentStage stage, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents SET stage=$stage,updated_at=$now
            WHERE task_id=$task AND stage=$from;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$from", (int)from);
        command.Parameters.AddWithValue("$stage", (int)stage);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
    }

    public async Task MarkFailedAsync(Guid taskId, string errorCode, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents SET stage=3,error_code=$error,updated_at=$now
            WHERE task_id=$task AND stage<3;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$error", errorCode);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);
    }

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
            WHERE stage<3 AND NOT EXISTS(SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_creation_intents.task_id);
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(Guid projectId, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task_id,project_id,repository,base_reference,base_commit,target,stage,error_code,created_at,updated_at
            FROM worktree_creation_intents WHERE project_id=$project AND stage IN (3,5) ORDER BY created_at;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        var items = new List<WorktreeCreationIntent>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            items.Add(new(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                (WorktreeCreationIntentStage)reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                DateTimeOffset.Parse(reader.GetString(8)),
                reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))));
        }
        return items;
    }

    public async Task AcknowledgeAsync(Guid taskId, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents SET stage=6,updated_at=$now
            WHERE task_id=$task AND stage IN (3,5);
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
    }
}
