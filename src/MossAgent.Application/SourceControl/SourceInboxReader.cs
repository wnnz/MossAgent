using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourceInboxReader(ISourceAccountRepository accounts, IPullRequestInboxClient client)
{
    public async Task<PullRequestInboxPage> ReadAsync(Guid accountId, long accountRevision,
        PullRequestInboxFilter filter, int page, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 1000 || !Enum.IsDefined(filter)) throw new SourceReviewException("invalid_arguments");
        var account = await ResolveAsync(accountId, accountRevision, cancellationToken).ConfigureAwait(false);
        var result = await client.InboxAsync(account, filter, page, cancellationToken).ConfigureAwait(false);
        _ = await ResolveAsync(accountId, accountRevision, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<SourceAccount> ResolveAsync(Guid accountId, long accountRevision, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.IsEnabled || account.Revision != accountRevision)
            throw new SourceReviewException("source_scope_denied");
        return account;
    }
}
