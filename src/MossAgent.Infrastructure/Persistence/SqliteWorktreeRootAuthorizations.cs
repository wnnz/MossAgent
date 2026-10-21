using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal sealed class SqliteWorktreeRootAuthorizations(SqliteConnectionFactory connections)
{
    public async Task<WorktreeRootAuthorization?> ReadAsync(Guid taskId, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id,grant_id,path,volume_id,file_id FROM worktree_root_authorizations WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(taskId, Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            new(reader.GetString(2), reader.GetString(3), reader.GetString(4))) : null;
    }

    public async Task InsertAsync(ProjectProfile project, WorktreeRootAuthorization grant, AgentTask? expected, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO worktree_root_authorizations(task_id,project_id,grant_id,path,volume_id,file_id)
            SELECT $task,$project,$grant,$path,$volume,$file
            WHERE EXISTS(SELECT 1 FROM projects WHERE id=$project AND authorization_revision=$revision)
            """ + (expected is null ? " AND NOT EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task);" : """
             AND EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task AND project_id=$project AND worktree_path=$path
                AND updated_at=$updated AND status=$status AND status NOT IN ($running,$approval));
            """);
        command.Parameters.AddWithValue("$task", grant.TaskId.ToString());
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$grant", grant.GrantId.ToString());
        command.Parameters.AddWithValue("$path", grant.Identity.Path);
        command.Parameters.AddWithValue("$volume", grant.Identity.VolumeId);
        command.Parameters.AddWithValue("$file", grant.Identity.FileId);
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        if (expected is not null)
        {
            command.Parameters.AddWithValue("$updated", expected.UpdatedAt.ToString("O"));
            command.Parameters.AddWithValue("$status", (int)expected.Status);
            command.Parameters.AddWithValue("$running", (int)AgentTaskStatus.Running);
            command.Parameters.AddWithValue("$approval", (int)AgentTaskStatus.WaitingForApproval);
        }
        try
        {
            if (await command.ExecuteNonQueryAsync(token) != 1) throw new WorktreeAuthorizationException();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19) { throw new WorktreeAuthorizationException(); }
        await transaction.CommitAsync(token);
    }
}
