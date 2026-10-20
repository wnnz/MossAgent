using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Application.SourceControl;

public sealed class SourceReviewReader(ISourceRepositoryGrants grants, ISourceAccountRepository accounts,
    IPullRequestClient client) : ISourceReviewReader
{
    private readonly SourceRepositoryAccess _access = new(grants, accounts);
    public async Task ValidateAsync(SourceRepositoryScope scope, CancellationToken cancellationToken) =>
        _ = await _access.ResolveAsync(scope, cancellationToken);

    public async Task<PullRequestPage> ListAsync(SourceRepositoryScope scope, int page, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 1000) throw new SourceReviewException("invalid_arguments");
        var account = await _access.ResolveAsync(scope, cancellationToken);
        var result = await client.ListAsync(account, scope, page, cancellationToken);
        await ValidateAsync(scope, cancellationToken);
        return result;
    }

    public async Task<PullRequestSummary> GetAsync(SourceRepositoryScope scope, int number, CancellationToken cancellationToken)
    {
        if (number <= 0) throw new SourceReviewException("invalid_arguments");
        var account = await _access.ResolveAsync(scope, cancellationToken);
        var result = await client.GetAsync(account, scope, number, cancellationToken);
        await ValidateAsync(scope, cancellationToken);
        return result;
    }

}
