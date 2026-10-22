using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;

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

    public async Task<long> ReauthorizeAsync(
        ProjectProfile project,
        AgentTask expected,
        WorktreeRootAuthorization previous,
        WorktreeRootAuthorization current,
        CancellationToken token)
    {
        if (expected.Id != previous.TaskId || expected.ProjectId != project.Id
            || expected.WorktreePath is null
            || !GitWorktreePaths.Same(expected.WorktreePath, previous.Identity.Path)
            || current.TaskId != previous.TaskId || current.ProjectId != previous.ProjectId
            || !GitWorktreePaths.Same(current.Identity.Path, previous.Identity.Path))
            throw new WorktreeAuthorizationException();
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var transaction = connection.BeginTransaction();
        var grantUpdated = await UpdateGrantAsync(
            connection, transaction, project, expected, previous, current, token);
        if (!grantUpdated) throw new WorktreeAuthorizationException();
        var revision = await AdvanceProjectRevisionAsync(
            connection, transaction, project, token);
        await InsertHistoryAsync(
            connection, transaction, project, previous, current, revision, token);
        await transaction.CommitAsync(token);
        return revision;
    }

    private static async Task<bool> UpdateGrantAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectProfile project,
        AgentTask expected,
        WorktreeRootAuthorization previous,
        WorktreeRootAuthorization current,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE worktree_root_authorizations
            SET grant_id=$new_grant,path=$path,volume_id=$volume,file_id=$file
            WHERE task_id=$task AND project_id=$project AND grant_id=$old_grant
              AND path=$path
              AND EXISTS(SELECT 1 FROM projects WHERE id=$project AND authorization_revision=$revision)
              AND EXISTS(
                  SELECT 1 FROM agent_tasks
                  WHERE id=$task AND project_id=$project AND worktree_path=$path
                    AND updated_at=$updated AND status=$status
                    AND status NOT IN ($running,$approval)
              );
            """;
        command.Parameters.AddWithValue("$new_grant", current.GrantId.ToString());
        command.Parameters.AddWithValue("$old_grant", previous.GrantId.ToString());
        command.Parameters.AddWithValue("$task", expected.Id.ToString());
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$path", current.Identity.Path);
        command.Parameters.AddWithValue("$volume", current.Identity.VolumeId);
        command.Parameters.AddWithValue("$file", current.Identity.FileId);
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        command.Parameters.AddWithValue("$updated", expected.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)expected.Status);
        command.Parameters.AddWithValue("$running", (int)AgentTaskStatus.Running);
        command.Parameters.AddWithValue("$approval", (int)AgentTaskStatus.WaitingForApproval);
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    private static async Task<long> AdvanceProjectRevisionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectProfile project,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE project_root_authorizations
            SET authorization_revision=$next
            WHERE project_id=$project AND authorization_revision=$revision;
            """;
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        command.Parameters.AddWithValue("$next", project.AuthorizationRevision + 1);
        var rootAuthorizations = await command.ExecuteNonQueryAsync(token);
        if (project.RootAuthorizations.Count > 0
            && rootAuthorizations != 1)
            throw new ProjectAuthorizationConflictException();
        command.CommandText = """
            UPDATE projects SET authorization_revision=authorization_revision+1
            WHERE id=$project AND authorization_revision=$revision;
            """;
        if (await command.ExecuteNonQueryAsync(token) != 1)
            throw new ProjectAuthorizationConflictException();
        return project.AuthorizationRevision + 1;
    }

    private static async Task InsertHistoryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProjectProfile project,
        WorktreeRootAuthorization previous,
        WorktreeRootAuthorization current,
        long revision,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO worktree_root_authorization_history(
                task_id,project_id,previous_grant_id,grant_id,path,
                previous_volume_id,previous_file_id,volume_id,file_id,
                project_revision,created_at)
            VALUES($task,$project,$old_grant,$new_grant,$path,
                $old_volume,$old_file,$volume,$file,$revision,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        command.Parameters.AddWithValue("$task", current.TaskId.ToString());
        command.Parameters.AddWithValue("$project", project.Id.ToString());
        command.Parameters.AddWithValue("$old_grant", previous.GrantId.ToString());
        command.Parameters.AddWithValue("$new_grant", current.GrantId.ToString());
        command.Parameters.AddWithValue("$path", current.Identity.Path);
        command.Parameters.AddWithValue("$old_volume", previous.Identity.VolumeId);
        command.Parameters.AddWithValue("$old_file", previous.Identity.FileId);
        command.Parameters.AddWithValue("$volume", current.Identity.VolumeId);
        command.Parameters.AddWithValue("$file", current.Identity.FileId);
        command.Parameters.AddWithValue("$revision", revision);
        await command.ExecuteNonQueryAsync(token);
    }
}
