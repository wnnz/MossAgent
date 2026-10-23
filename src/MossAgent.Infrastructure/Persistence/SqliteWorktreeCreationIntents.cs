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
                task_id,project_id,repository,base_reference,base_commit,target,state,created_at)
            SELECT $task,$project,$repository,$reference,$commit,$target,0,$now
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
            UPDATE worktree_creation_intents SET volume_id=$volume,file_id=$file
            WHERE task_id=$task AND state=0 AND target=$target;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$volume", identity.VolumeId);
        command.Parameters.AddWithValue("$file", identity.FileId);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
    }
}
