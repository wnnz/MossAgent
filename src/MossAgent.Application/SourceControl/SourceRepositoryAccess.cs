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
}
