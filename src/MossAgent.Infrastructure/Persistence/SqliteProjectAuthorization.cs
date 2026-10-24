using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Persistence;

internal sealed class SqliteProjectAuthorization(SqliteConnectionFactory connections, IProjectAuthorizationChanges? changes,
    IRootIdentitySessionFactory? roots = null)
{
    public Task<ProjectProfile?> ReadAsync(Guid projectId, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return await ReadAsync(connection, null, projectId, cancellationToken);
    }, cancellationToken);

    public Task<ProjectProfile> SaveAsync(ProjectProfile project, CancellationToken cancellationToken, bool confirm = false)
    {
        var snapshot = project.Snapshot();
        if (snapshot.AuthorizationRevision <= 0) throw new ProjectAuthorizationConflictException();
        return Task.Run(() => SaveCoreAsync(snapshot, cancellationToken, confirm), cancellationToken);
    }

    private async Task<ProjectProfile> SaveCoreAsync(ProjectProfile project, CancellationToken cancellationToken, bool confirm)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        var current = await ReadAsync(connection, null, project.Id, cancellationToken);
        if (current is not null && current.AuthorizationRevision != project.AuthorizationRevision
            || current is null && (confirm || project.AuthorizationRevision != 1)) throw new ProjectAuthorizationConflictException();
        if (confirm && roots is null) throw new ProjectRootAuthorizationException();
        using var enrollment = roots is null ? null
            : await ProjectRootEnrollment.PrepareAsync(roots, current, project, confirm, cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO projects(id,name,primary_directory,authorized_directories_json,created_at)
            SELECT $id,$name,$primary,$roots,$created
            WHERE $revision=1 OR EXISTS(SELECT 1 FROM projects WHERE id=$id)
            ON CONFLICT(id) DO UPDATE SET name=$name,primary_directory=$primary,authorized_directories_json=$roots
            WHERE projects.authorization_revision=$revision;
            """;
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$primary", project.PrimaryDirectory);
        command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(project.AuthorizedDirectories));
        command.Parameters.AddWithValue("$created", project.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$revision", project.AuthorizationRevision);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) throw new ProjectAuthorizationConflictException();
        if (confirm)
        {
            command.CommandText = "UPDATE projects SET authorization_revision=authorization_revision+1 WHERE id=$id;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        var saved = await ReadAsync(connection, transaction, project.Id, cancellationToken)
            ?? throw new ProjectAuthorizationConflictException();
        if (enrollment is not null)
        {
            await SqliteProjectRootAuthorizations.WriteAsync(connection, transaction, saved, enrollment.Authorizations, cancellationToken);
            saved = saved with { RootAuthorizations = enrollment.Authorizations.ToArray() };
        }
        await transaction.CommitAsync(cancellationToken);
        changes?.Publish(saved.Id, saved.AuthorizationRevision);
        return saved;
    }

    private static async Task<ProjectProfile?> ReadAsync(SqliteConnection connection, SqliteTransaction? transaction,
        Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT p.id,p.name,p.primary_directory,p.authorized_directories_json,p.created_at,p.authorization_revision,r.roots_json
            FROM projects p LEFT JOIN project_root_authorizations r ON r.project_id=p.id AND r.authorization_revision=p.authorization_revision WHERE p.id=$id;
            """;
        command.Parameters.AddWithValue("$id", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectProfile(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                JsonSerializer.Deserialize<string[]>(reader.GetString(3)) ?? [], DateTimeOffset.Parse(reader.GetString(4)))
                { AuthorizationRevision = reader.GetInt64(5), RootAuthorizations = SqliteProjectRootAuthorizations.Read(reader, 6) } : null;
    }
}
