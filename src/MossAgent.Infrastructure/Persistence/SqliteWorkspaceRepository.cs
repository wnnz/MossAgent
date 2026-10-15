using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorkspaceRepository(SqliteConnectionFactory connections)
    : IWorkspaceRepository
{
    private readonly SqliteWorkspaceWriter _writer = new(connections);
    private readonly SqliteTaskRenameWriter _renameWriter = new(connections);

    public async Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,primary_directory,authorized_directories_json,created_at FROM projects ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ProjectProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProjectProfile(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                JsonSerializer.Deserialize<string[]>(reader.GetString(3)) ?? [],
                DateTimeOffset.Parse(reader.GetString(4))));
        }

        return results;
    }

    public async Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO projects(id,name,primary_directory,authorized_directories_json,created_at)
            VALUES($id,$name,$primary,$roots,$created)
            ON CONFLICT(id) DO UPDATE SET name=$name,primary_directory=$primary,authorized_directories_json=$roots;
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$primary", project.PrimaryDirectory);
        command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(project.AuthorizedDirectories));
        command.Parameters.AddWithValue("$created", project.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentTask>> GetTasksAsync(
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task.id,task.project_id,task.title,task.approval_policy,task.status,
                   task.created_at,task.updated_at,task.worktree_path,
                   pinned.task_id IS NOT NULL, failure.reason
            FROM agent_tasks task
            LEFT JOIN pinned_agent_tasks pinned ON pinned.task_id=task.id
            LEFT JOIN agent_task_failures failure ON failure.task_id=task.id
            WHERE task.project_id IS $project
              AND NOT EXISTS (
                  SELECT 1 FROM archived_agent_tasks archived
                  WHERE archived.task_id=task.id)
            ORDER BY task.updated_at DESC;
            """;
        command.Parameters.AddWithValue("$project", (object?)projectId?.ToString() ?? DBNull.Value);
        return await SqliteWorkspaceTaskReader.ReadAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task.id,task.project_id,task.title,task.approval_policy,task.status,
                   task.created_at,task.updated_at,task.worktree_path,
                   pinned.task_id IS NOT NULL, failure.reason
            FROM agent_tasks task
            INNER JOIN archived_agent_tasks archived ON archived.task_id=task.id
            LEFT JOIN pinned_agent_tasks pinned ON pinned.task_id=task.id
            LEFT JOIN agent_task_failures failure ON failure.task_id=task.id
            WHERE task.project_id IS $project
            ORDER BY archived.archived_at DESC;
            """;
        command.Parameters.AddWithValue("$project", (object?)projectId?.ToString() ?? DBNull.Value);
        return await SqliteWorkspaceTaskReader.ReadAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentTask>> GetPinnedTasksAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT task.id,task.project_id,task.title,task.approval_policy,task.status,
                   task.created_at,task.updated_at,task.worktree_path,1,failure.reason
            FROM pinned_agent_tasks pinned
            INNER JOIN agent_tasks task ON task.id=pinned.task_id
            LEFT JOIN agent_task_failures failure ON failure.task_id=task.id
            WHERE NOT EXISTS (
                SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=task.id)
            ORDER BY pinned.pinned_at DESC;
            """;
        return await SqliteWorkspaceTaskReader.ReadAsync(command, cancellationToken);
    }

    public Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken) =>
        _writer.SaveAsync(task, [], cancellationToken);

    public Task<AgentTask?> RenameTaskAsync(AgentTask expectedTask, string title, CancellationToken cancellationToken) =>
        _renameWriter.RenameAsync(expectedTask, title, cancellationToken);

    public Task SaveTaskWithMessagesAsync(AgentTask task, IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken) => _writer.SaveAsync(task, messages, cancellationToken);

    public async Task<bool> SetTaskPinnedAsync(
        Guid taskId, bool isPinned, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = isPinned
            ? """
              INSERT INTO pinned_agent_tasks(task_id,pinned_at)
              SELECT task.id,$pinned FROM agent_tasks task
              WHERE task.id=$task AND NOT EXISTS (
                  SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=task.id)
              ON CONFLICT(task_id) DO UPDATE SET pinned_at=$pinned;
              """
            : """
              DELETE FROM pinned_agent_tasks WHERE task_id=$task
              AND NOT EXISTS (
                  SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=$task);
              """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$pinned", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task SetTaskArchivedAsync(
        Guid taskId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = isArchived
            ? """
              INSERT INTO archived_agent_tasks(task_id,archived_at)
              VALUES($task,$archived)
              ON CONFLICT(task_id) DO UPDATE SET archived_at=$archived;
              """
            : "DELETE FROM archived_agent_tasks WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$archived", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,task_id,role,content,created_at,tool_call_id
            FROM messages WHERE task_id=$task ORDER BY created_at,rowid;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ConversationMessage>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ConversationMessage(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                (MessageRole)reader.GetInt32(2), reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return results;
    }

    public Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken) => _writer.AppendAsync(message, cancellationToken);

}
