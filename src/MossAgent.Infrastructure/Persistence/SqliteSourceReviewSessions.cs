using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteSourceReviewSessions(SqliteConnectionFactory connections) : ISourceReviewSessions
{
    public async Task<Guid> GetOrCreateAsync(SourceRepositoryScope scope, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var check = connection.CreateCommand();
        check.Transaction = transaction;
        check.CommandText = """
            SELECT COUNT(*) FROM source_repository_grants grant_scope JOIN source_accounts account ON account.id=grant_scope.account_id
            WHERE grant_scope.id=$grant AND account.id=$account AND account.revision=$revision AND account.is_enabled=1
              AND grant_scope.account_revision=$revision AND grant_scope.owner=$owner AND grant_scope.repository=$repository;
            """;
        check.Parameters.AddWithValue("$grant", scope.GrantId.ToString());
        check.Parameters.AddWithValue("$account", scope.AccountId.ToString());
        check.Parameters.AddWithValue("$revision", scope.AccountRevision);
        check.Parameters.AddWithValue("$owner", scope.Owner);
        check.Parameters.AddWithValue("$repository", scope.Repository);
        if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new SourceReviewException("source_scope_denied");
        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT task_id FROM source_review_sessions WHERE grant_id=$grant;";
        existing.Parameters.AddWithValue("$grant", scope.GrantId.ToString());
        var previous = (string?)await existing.ExecuteScalarAsync(cancellationToken);
        if (previous is not null) { await transaction.CommitAsync(cancellationToken); return Guid.Parse(previous); }
        var id = Guid.NewGuid();
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO agent_tasks(id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path)
            VALUES($id,NULL,$title,$policy,$status,$now,$now,NULL);
            INSERT INTO source_review_sessions(task_id,grant_id) VALUES($id,$grant);
            """;
        insert.Parameters.AddWithValue("$id", id.ToString());
        insert.Parameters.AddWithValue("$grant", scope.GrantId.ToString());
        insert.Parameters.AddWithValue("$title", $"{scope.Owner}/{scope.Repository}");
        insert.Parameters.AddWithValue("$policy", (int)ApprovalPolicy.ReadOnly);
        insert.Parameters.AddWithValue("$status", (int)AgentTaskStatus.Completed);
        insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }
}
