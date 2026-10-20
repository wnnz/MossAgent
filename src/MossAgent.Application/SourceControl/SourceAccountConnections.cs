using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public sealed class SourceAccountConnections(
    ISourceAccountRepository accounts, ISourceAccountIdentityClient identities)
{
    public async Task<SourceAccountIdentity> RefreshIdentityAsync(Guid accountId,
        long expectedRevision, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || !account.IsEnabled)
            throw new SourceConnectionException(SourceConnectionFailure.AccountUnavailable);
        if (account.Revision != expectedRevision)
            throw new SourceConnectionException(SourceConnectionFailure.AccountChanged);
        var identity = await identities.GetIdentityAsync(account, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var current = await accounts.GetAsync(accountId, cancellationToken);
        if (current is null || !current.IsEnabled || current.Revision != account.Revision)
            throw new SourceConnectionException(SourceConnectionFailure.AccountChanged);
        return identity;
    }
}
