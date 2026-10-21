using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationFollowupAuthorization
{
    public static async Task ValidateAsync(SqliteConnection connection, SqliteTransaction transaction,
        PreparedConversationFollowup expected, ConversationFollowup item, CancellationToken cancellationToken)
    {
        if (expected.Task.Id != item.TaskId || expected.Task.ProjectId != item.ProjectId
            || expected.Task.WorktreePath != item.WorktreePath || expected.Project?.Id != item.ProjectId)
            throw new ConversationFollowupException("FollowupScopeChanged");
        if (expected.Provider.Id != item.Model.ProviderId || !expected.Provider.IsEnabled
            || (expected.Model with { SupportsImages = item.Model.SupportsImages, SupportsFiles = item.Model.SupportsFiles }) != item.Model)
            throw new ConversationFollowupException("FollowupModelUnavailable");
        await ValidateProjectAsync(connection, transaction, expected.Project, item, cancellationToken);
        await ValidateModelAsync(connection, transaction, expected, cancellationToken);
        if (item.AgentProfileId is not { } agentId) return;
        await using var agent = connection.CreateCommand();
        agent.Transaction = transaction;
        agent.CommandText = "SELECT COUNT(*) FROM agent_profiles WHERE id=$id AND is_enabled=1;";
        agent.Parameters.AddWithValue("$id", agentId.ToString());
        if (Convert.ToInt64(await agent.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new ConversationFollowupException("FollowupAgentUnavailable");
    }

    private static async Task ValidateProjectAsync(SqliteConnection connection, SqliteTransaction transaction,
        ProjectProfile? project, ConversationFollowup item, CancellationToken cancellationToken)
    {
        if (project is null) return;
        var roots = item.WorktreePath is null ? project.AuthorizedDirectories.ToArray() : project.AuthorizedDirectories
            .Where(path => !path.Equals(project.PrimaryDirectory, StringComparison.OrdinalIgnoreCase)).Prepend(item.WorktreePath).ToArray();
        if (!new HashSet<string>(roots, StringComparer.Ordinal).SetEquals(item.AuthorizedRoots))
            throw new ConversationFollowupException("FollowupScopeChanged");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM projects WHERE id=$id AND primary_directory=$primary
              AND authorized_directories_json=$roots AND authorization_revision=$revision;
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$primary", project.PrimaryDirectory);
        command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(project.AuthorizedDirectories));
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new ConversationFollowupException("FollowupScopeChanged");
    }

    private static async Task ValidateModelAsync(SqliteConnection connection, SqliteTransaction transaction,
        PreparedConversationFollowup expected, CancellationToken cancellationToken)
    {
        var provider = expected.Provider;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM providers p JOIN models m ON m.provider_id=p.id
            WHERE p.id=$provider AND p.is_enabled=1 AND p.protocol=$protocol AND p.base_uri=$uri
              AND p.api_key=$key AND p.proxy_id IS $proxy AND p.headers_json=$headers
              AND m.id=$model AND m.model_id=$modelName AND m.is_enabled=1
              AND m.supports_images >= $images AND m.supports_files >= $files;
            """;
        command.Parameters.AddWithValue("$provider", provider.Id.ToString());
        command.Parameters.AddWithValue("$protocol", (int)provider.Protocol);
        command.Parameters.AddWithValue("$uri", provider.BaseUri.ToString());
        command.Parameters.AddWithValue("$key", provider.ApiKey);
        command.Parameters.AddWithValue("$proxy", (object?)provider.ProxyId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$headers", JsonSerializer.Serialize(provider.Headers));
        command.Parameters.AddWithValue("$model", expected.Model.Id.ToString());
        command.Parameters.AddWithValue("$modelName", expected.Model.ModelId);
        command.Parameters.AddWithValue("$images", expected.Model.SupportsImages);
        command.Parameters.AddWithValue("$files", expected.Model.SupportsFiles);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new ConversationFollowupException("FollowupModelUnavailable");
    }
}
