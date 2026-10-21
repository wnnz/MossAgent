using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Application.Attachments;
using MossAgent.Application.Authorization;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorkspaceRepository(SqliteConnectionFactory connections,
    AppDataPaths? paths = null, IAttachmentImageDecoder? images = null, IProjectAuthorizationChanges? authorizationChanges = null,
    IRootIdentitySessionFactory? rootIdentities = null)
    : IWorkspaceRepository, IConversationSubmissionRepository, IProjectAuthorizationReader, IProjectAuthorizationWriter, IProjectAuthorizationConfirmation
{
    private readonly SqliteWorkspaceWriter _writer = new(connections);
    private readonly SqliteTaskRenameWriter _renameWriter = new(connections);
    private readonly SqliteConversationDraftWriter _draftWriter = new(paths, images);
    private readonly SqliteProjectAuthorization _projects = new(connections, authorizationChanges, rootIdentities);
    public bool CanConfirmRootObjects => rootIdentities is not null;

    public Task<ProjectProfile> ConfirmRootObjectsAsync(ProjectProfile expected, CancellationToken cancellationToken) =>
        _projects.SaveAsync(expected, cancellationToken, confirm: true);

    public bool SupportsDrafts(IConversationDraftRepository drafts) =>
        drafts is SqliteConversationDraftRepository sqlite && sqlite.UsesConnections(connections);

    public Task SaveSubmissionAsync(AgentTask task, ConversationMessage message,
        IReadOnlyList<ConversationDraftUpdate> drafts, CancellationToken cancellationToken)
    {
        var snapshot = drafts.Select(update => update with { Attachments = update.AttachmentItems.ToArray() }).ToArray();
        _draftWriter.Validate(snapshot);
        var target = new ConversationDraftScope(task.ProjectId, task.Id);
        if (message.TaskId != task.Id || message.Role != MessageRole.User || snapshot.Length is < 1 or > 2
            || snapshot.Any(update => update.Scope.ProjectId != task.ProjectId
                || update.Scope.TaskId is { } id && id != task.Id)
            || !snapshot.Any(update => update.Scope == target))
            throw new ArgumentException("Invalid conversation submission scope.");
        return Task.Run(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            await _writer.SaveAsync(task, [message], deadline.Token, async (connection, transaction, token) =>
            {
                if (!await _draftWriter.WriteAsync(connection, transaction, snapshot, token))
                    throw new ConversationDraftConflictException();
            });
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id,p.name,p.primary_directory,p.authorized_directories_json,p.created_at,p.authorization_revision,r.roots_json
            FROM projects p LEFT JOIN project_root_authorizations r ON r.project_id=p.id AND r.authorization_revision=p.authorization_revision ORDER BY p.name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<ProjectProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ProjectProfile(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                JsonSerializer.Deserialize<string[]>(reader.GetString(3)) ?? [],
                DateTimeOffset.Parse(reader.GetString(4)))
            { AuthorizationRevision = reader.GetInt64(5), RootAuthorizations = SqliteProjectRootAuthorizations.Read(reader, 6) });
        }

        return results;
    }

    public Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken) => _projects.SaveAsync(project, cancellationToken);

    public Task<ProjectProfile> SaveAuthorizationAsync(ProjectProfile project, CancellationToken cancellationToken) =>
        _projects.SaveAsync(project, cancellationToken);

    public Task<ProjectProfile?> ReadAuthorizationAsync(Guid projectId, CancellationToken cancellationToken) =>
        _projects.ReadAsync(projectId, cancellationToken);

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
              AND NOT EXISTS (SELECT 1 FROM source_review_sessions review_session WHERE review_session.task_id=task.id)
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
              AND NOT EXISTS (SELECT 1 FROM source_review_sessions review_session WHERE review_session.task_id=task.id)
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
              AND NOT EXISTS (SELECT 1 FROM source_review_sessions review_session WHERE review_session.task_id=task.id)
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
        await using var transaction = connection.BeginTransaction(deferred: true);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id,task_id,role,content,created_at,tool_call_id
            FROM messages WHERE task_id=$task ORDER BY created_at,rowid;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        var results = new List<ConversationMessage>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ConversationMessage(
                    Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                    (MessageRole)reader.GetInt32(2), reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
        }
        return await SqliteConversationAttachments.RestoreAsync(connection, transaction, taskId, results, cancellationToken);
    }

    public Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken) => _writer.AppendAsync(message, cancellationToken);

}
