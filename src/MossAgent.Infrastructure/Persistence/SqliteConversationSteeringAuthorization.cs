using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringAuthorization
{
    public static async Task ValidateAsync(SqliteConnection connection, SqliteTransaction transaction, Guid taskId,
        ConversationSteeringAuthorization? authorization, CancellationToken token)
    {
        // Storage-only callers do not run models. Production sessions always supply a captured scope.
        if (authorization is null) return;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM agent_tasks WHERE id=$task AND project_id IS $project;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", (object?)authorization.Project?.Id.ToString() ?? DBNull.Value);
        await RequireAsync(command, token);
        if (authorization.Project is { } project)
        {
            command.CommandText = "SELECT COUNT(*) FROM projects WHERE id=$project AND primary_directory=$primary AND authorized_directories_json=$roots;";
            command.Parameters.AddWithValue("$primary", project.PrimaryDirectory);
            command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(project.AuthorizedDirectories));
            await RequireAsync(command, token);
        }
        await ValidateSourceAsync(command, taskId, authorization.SourceReview, token);
    }

    private static async Task ValidateSourceAsync(SqliteCommand command, Guid taskId, SourceReviewChatBinding? review, CancellationToken token)
    {
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$task", taskId.ToString());
        if (review is null)
        {
            command.CommandText = "SELECT NOT EXISTS(SELECT 1 FROM source_review_chats WHERE task_id=$task);";
            await RequireAsync(command, token);
            return;
        }
        review.Validate();
        if (review.TaskId != taskId) throw new ConversationSteeringException("SteeringScopeChanged");
        command.CommandText = """
            SELECT COUNT(*) FROM source_review_chats chat
            JOIN source_repository_grants grant_scope ON grant_scope.id=chat.grant_id
            JOIN source_accounts account ON account.id=chat.account_id
            WHERE chat.task_id=$task AND chat.grant_id=$grant AND chat.account_id=$account AND chat.account_revision=$revision
              AND chat.owner=$owner AND chat.repository=$repository AND chat.number=$number
              AND chat.base_revision=$base AND chat.head_revision=$head AND chat.context=$context AND chat.mode=$mode
              AND grant_scope.account_id=$account AND grant_scope.account_revision=$revision
              AND grant_scope.owner=$owner AND grant_scope.repository=$repository
              AND account.revision=$revision AND account.is_enabled=1;
            """;
        command.Parameters.AddWithValue("$grant", review.Scope.GrantId.ToString());
        command.Parameters.AddWithValue("$account", review.Scope.AccountId.ToString());
        command.Parameters.AddWithValue("$revision", review.Scope.AccountRevision);
        command.Parameters.AddWithValue("$owner", review.Scope.Owner);
        command.Parameters.AddWithValue("$repository", review.Scope.Repository);
        command.Parameters.AddWithValue("$number", review.Revision.Number);
        command.Parameters.AddWithValue("$base", review.Revision.BaseRevision);
        command.Parameters.AddWithValue("$head", review.Revision.HeadRevision);
        command.Parameters.AddWithValue("$context", review.Context);
        command.Parameters.AddWithValue("$mode", (int)review.Mode);
        await RequireAsync(command, token);
    }

    private static async Task RequireAsync(SqliteCommand command, CancellationToken token)
    {
        if (Convert.ToInt64(await command.ExecuteScalarAsync(token)) != 1)
            throw new ConversationSteeringException("SteeringScopeChanged");
    }
}
