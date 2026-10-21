using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteProjectRootAuthorizations
{
    public static IReadOnlyList<ProjectRootAuthorization> Read(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? [] : JsonSerializer.Deserialize<ProjectRootAuthorization[]>(reader.GetString(index)) ?? [];

    public static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        ProjectProfile saved, IReadOnlyList<ProjectRootAuthorization> roots, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO project_root_authorizations(project_id,authorization_revision,roots_json) VALUES($id,$revision,$roots)
            ON CONFLICT(project_id) DO UPDATE SET authorization_revision=$revision,roots_json=$roots;
            """;
        command.Parameters.AddWithValue("$id", saved.Id.ToString());
        command.Parameters.AddWithValue("$revision", saved.AuthorizationRevision);
        command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(roots));
        await command.ExecuteNonQueryAsync(token);
    }
}
