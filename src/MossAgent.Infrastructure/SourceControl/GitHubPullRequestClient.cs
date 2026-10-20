using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestClient(ISourceAccountHttpClientFactory clients) : IPullRequestClient
{
    private readonly GitHubSourceTransport _transport = new(clients);

    public async Task<PullRequestPage> ListAsync(SourceAccount account, SourceRepositoryScope scope, int page,
        CancellationToken cancellationToken)
    {
        Validate(account, scope);
        if (page is < 1 or > 1000) throw new SourceReviewException("invalid_arguments");
        var body = await _transport.GetAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/pulls?state=open&sort=updated&direction=desc&per_page=20&page={page}", cancellationToken);
        try { return GitHubPullRequestParser.ParsePage(body, scope, account.AccessToken, page); }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    public async Task<PullRequestSummary> GetAsync(SourceAccount account, SourceRepositoryScope scope, int number,
        CancellationToken cancellationToken)
    {
        Validate(account, scope);
        if (number <= 0) throw new SourceReviewException("invalid_arguments");
        var body = await _transport.GetAsync(account, $"repos/{scope.Owner}/{scope.Repository}/pulls/{number}", cancellationToken);
        try { return GitHubPullRequestParser.ParseDetail(body, scope, account.AccessToken, number); }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    private static void Validate(SourceAccount account, SourceRepositoryScope scope)
    {
        if (!account.IsEnabled || account.Id != scope.AccountId || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
    }
}
