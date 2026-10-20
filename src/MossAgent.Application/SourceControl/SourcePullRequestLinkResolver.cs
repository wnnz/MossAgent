using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourcePullRequestLinkResolver(ISourceAccountRepository accounts, ISourceRepositoryGrants grants)
{
    private readonly SourceRepositoryAccess _access = new(grants, accounts);
    public async Task<SourcePullRequestTarget> ResolveAsync(Guid accountId, long accountRevision, string value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        SourcePullRequestLink link;
        try { link = SourcePullRequestLink.Parse(value); }
        catch (ArgumentException) { throw new SourceReviewException("source_invalid_pr_url"); }
        var account = await accounts.GetAsync(accountId, token).ConfigureAwait(false);
        if (account is null || !account.IsEnabled || account.Revision != accountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (!link.MatchesEndpoint(account.ApiEndpoint)) throw new SourceReviewException("source_url_account_mismatch");
        var scopes = await grants.GetAllAsync(token).ConfigureAwait(false);
        var scope = scopes.FirstOrDefault(scope => scope.AccountId == account.Id && scope.AccountRevision == account.Revision
            && scope.Owner.Equals(link.Owner, StringComparison.OrdinalIgnoreCase)
            && scope.Repository.Equals(link.Repository, StringComparison.OrdinalIgnoreCase));
        if (scope is null) throw new SourceReviewException("source_url_repository_not_granted");
        await _access.ResolveAsync(scope, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return new(scope, link.Number);
    }
}
