using System.Text.Json;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorkspaceRepository(SqliteConnectionFactory connections)
    : IWorkspaceRepository
{
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
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path
            FROM agent_tasks WHERE project_id=$project ORDER BY updated_at DESC;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AgentTask>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AgentTask(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2),
                (ApprovalPolicy)reader.GetInt32(3), (AgentTaskStatus)reader.GetInt32(4),
                DateTimeOffset.Parse(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return results;
    }

    public async Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO agent_tasks(id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path)
            VALUES($id,$project,$title,$approval,$status,$created,$updated,$worktree)
            ON CONFLICT(id) DO UPDATE SET title=$title,approval_policy=$approval,status=$status,
            updated_at=$updated,worktree_path=$worktree;
            """;
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$project", task.ProjectId.ToString());
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$approval", (int)task.ApprovalPolicy);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$created", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", task.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$worktree", (object?)task.WorktreePath ?? DBNull.Value);
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
            FROM messages WHERE task_id=$task ORDER BY created_at;
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

    public async Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO messages(id,task_id,role,content,created_at,tool_call_id)
            VALUES($id,$task,$role,$content,$created,$tool);
            """;
        command.Parameters.AddWithValue("$id", message.Id.ToString());
        command.Parameters.AddWithValue("$task", message.TaskId.ToString());
        command.Parameters.AddWithValue("$role", (int)message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$created", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$tool", (object?)message.ToolCallId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

