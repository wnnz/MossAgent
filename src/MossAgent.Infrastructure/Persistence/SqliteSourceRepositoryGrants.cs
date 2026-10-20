using Microsoft.Data.Sqlite;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSourceRepositoryGrants(SqliteConnectionFactory connections) : ISourceRepositoryGrants
{
    public async Task<IReadOnlyList<SourceRepositoryScope>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,account_id,account_revision,owner,repository FROM source_repository_grants ORDER BY owner,repository,id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<SourceRepositoryScope>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(Read(reader));
        return results;
    }

    public async Task<SourceRepositoryScope?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,account_id,account_revision,owner,repository FROM source_repository_grants WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task AddAsync(SourceRepositoryScope scope, CancellationToken cancellationToken)
    {
        if (scope.GrantId == Guid.Empty || scope.AccountId == Guid.Empty || scope.AccountRevision <= 0
            || SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO source_repository_grants(id,account_id,account_revision,owner,repository)
            SELECT $id,$account,$revision,$owner,$repository FROM source_accounts
            WHERE id=$account AND revision=$revision AND is_enabled=1
              AND NOT EXISTS (SELECT 1 FROM source_repository_revocations WHERE id=$id);
            """;
        command.Parameters.AddWithValue("$id", scope.GrantId.ToString());
        command.Parameters.AddWithValue("$account", scope.AccountId.ToString());
        command.Parameters.AddWithValue("$revision", scope.AccountRevision);
        command.Parameters.AddWithValue("$owner", scope.Owner);
        command.Parameters.AddWithValue("$repository", scope.Repository);
        try
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new SourceReviewException("source_scope_denied");
        }
        catch (SqliteException) { throw new SourceReviewException("source_scope_denied"); }
    }

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO source_repository_revocations(id) VALUES($id);
            DELETE FROM source_repository_grants WHERE id=$id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static SourceRepositoryScope Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetInt64(2),
        reader.GetString(3), reader.GetString(4));
}
