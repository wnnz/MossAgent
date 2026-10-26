using Microsoft.Data.Sqlite;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSourceReviewChats(SqliteConnectionFactory connections) : ISourceReviewChats
{
    public async Task<AgentTask> OpenAsync(SourceReviewChatBinding binding, string title, CancellationToken cancellationToken)
    {
        binding.Validate();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256) throw new SourceReviewException("invalid_arguments");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await ValidateAsync(connection, transaction, binding.Scope, cancellationToken);
        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT task_id FROM source_review_chats WHERE grant_id=$grant AND number=$number AND base_revision=$base AND head_revision=$head AND mode=$mode;";
        Bind(existing, binding);
        var id = (string?)await existing.ExecuteScalarAsync(cancellationToken);
        if (id is null)
        {
            id = binding.TaskId.ToString();
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO agent_tasks(id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path)
                VALUES($task,NULL,$title,$policy,$status,$now,$now,NULL);
                INSERT INTO source_review_chats(task_id,grant_id,account_id,account_revision,owner,repository,number,base_revision,head_revision,context,mode)
                VALUES($task,$grant,$account,$revision,$owner,$repository,$number,$base,$head,$context,$mode);
                """;
            Bind(insert, binding);
            insert.Parameters.AddWithValue("$task", id); insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$policy", (int)ApprovalPolicy.ReadOnly);
            insert.Parameters.AddWithValue("$status", (int)AgentTaskStatus.Completed);
            insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            insert.Parameters.AddWithValue("$context", binding.Context);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await using var read = connection.CreateCommand(); read.Transaction = transaction;
        read.CommandText = """
            DELETE FROM archived_agent_tasks WHERE task_id=$id;
            SELECT task.id,task.project_id,task.title,task.approval_policy,task.status,task.created_at,task.updated_at,task.worktree_path,
                   pinned.task_id IS NOT NULL,failure.reason FROM agent_tasks task
            LEFT JOIN pinned_agent_tasks pinned ON pinned.task_id=task.id
            LEFT JOIN agent_task_failures failure ON failure.task_id=task.id WHERE task.id=$id;
            """;
        read.Parameters.AddWithValue("$id", id);
        var tasks = await SqliteWorkspaceTaskReader.ReadAsync(read, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tasks.Single();
    }

    public async Task<SourceReviewChatBinding?> GetAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create(); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT chat.grant_id,chat.account_id,chat.account_revision,chat.owner,chat.repository,
                   chat.number,chat.base_revision,chat.head_revision,chat.context,chat.mode,
                   IFNULL(grant.can_write,0)
            FROM source_review_chats chat
            LEFT JOIN source_repository_grants grant ON grant.id=chat.grant_id
            WHERE chat.task_id=$task;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var binding = new SourceReviewChatBinding(taskId, new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            reader.GetInt64(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(10) != 0),
            new(reader.GetInt32(5), reader.GetString(6), reader.GetString(7)), reader.GetString(8),
            (SourceReviewChatMode)reader.GetInt32(9));
        binding.Validate(); return binding;
    }

    private static async Task ValidateAsync(SqliteConnection connection, SqliteTransaction transaction, SourceRepositoryScope scope, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM source_repository_grants grant_scope JOIN source_accounts account ON account.id=grant_scope.account_id
            WHERE grant_scope.id=$grant AND account.id=$account AND account.revision=$revision AND account.is_enabled=1
              AND grant_scope.account_revision=$revision AND grant_scope.owner=$owner AND grant_scope.repository=$repository;
            """;
        BindScope(command, scope);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) != 1) throw new SourceReviewException("source_scope_denied");
    }

    private static void BindScope(SqliteCommand command, SourceRepositoryScope scope)
    {
        command.Parameters.AddWithValue("$grant", scope.GrantId.ToString()); command.Parameters.AddWithValue("$account", scope.AccountId.ToString());
        command.Parameters.AddWithValue("$revision", scope.AccountRevision); command.Parameters.AddWithValue("$owner", scope.Owner);
        command.Parameters.AddWithValue("$repository", scope.Repository);
    }

    private static void Bind(SqliteCommand command, SourceReviewChatBinding binding)
    {
        BindScope(command, binding.Scope);
        command.Parameters.AddWithValue("$number", binding.Revision.Number);
        command.Parameters.AddWithValue("$base", binding.Revision.BaseRevision); command.Parameters.AddWithValue("$head", binding.Revision.HeadRevision);
        command.Parameters.AddWithValue("$mode", (int)binding.Mode);
    }
}
