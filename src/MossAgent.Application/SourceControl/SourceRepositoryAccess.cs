using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourceRepositoryAccess(ISourceRepositoryGrants grants, ISourceAccountRepository accounts)
{
    public async Task<SourceAccount> ResolveAsync(SourceRepositoryScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var grant = await grants.GetAsync(scope.GrantId, cancellationToken);
        if (grant != scope) throw new SourceReviewException("source_scope_denied");
        var account = await accounts.GetAsync(scope.AccountId, cancellationToken);
        if (account is null || !account.IsEnabled || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        return account;
    }

    // Write resolution is intentionally a separate port: a read-only grant can never
    // satisfy it, and the grant row is re-read after the read double-check so a
    // revocation between the two checks still fails closed.
    public async Task<SourceAccount> ResolveWriteAsync(SourceRepositoryScope scope, CancellationToken cancellationToken)
    {
        var account = await ResolveAsync(scope, cancellationToken);
        var grant = await grants.GetAsync(scope.GrantId, cancellationToken);
        if (grant is not { CanWrite: true } || grant != scope) throw new SourceReviewException("source_scope_denied");
        return account;
    }
}
