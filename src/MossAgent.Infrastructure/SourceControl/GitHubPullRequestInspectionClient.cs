using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestInspectionClient(ISourceAccountHttpClientFactory clients) : IPullRequestInspectionClient
{
    private readonly GitHubSourceTransport _transport = new(clients);

    public Task<PullRequestInspectionPage<PullRequestFile>> FilesAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, int page, CancellationToken cancellationToken)
    {
        if (page > 150) throw new SourceReviewException("invalid_arguments");
        return ReadAsync(account, scope, revision, page, $"pulls/{revision.Number}/files",
            body => GitHubPullRequestFileParser.Parse(body, revision, page, account.AccessToken), cancellationToken);
    }

    public Task<PullRequestInspectionPage<PullRequestComment>> CommentsAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken)
    {
        if (kind is not ("conversation" or "inline")) throw new SourceReviewException("invalid_arguments");
        return ReadAsync(account, scope, revision, page, $"{(kind == "inline" ? "pulls" : "issues")}/{revision.Number}/comments",
            body => GitHubPullRequestCommentParser.Parse(body, account, scope, revision, kind, page), cancellationToken);
    }

    public Task<PullRequestInspectionPage<PullRequestCheck>> ChecksAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken)
    {
        if (kind is not ("runs" or "statuses")) throw new SourceReviewException("invalid_arguments");
        return ReadAsync(account, scope, revision, page, $"commits/{revision.HeadRevision}/{(kind == "runs" ? "check-runs" : "status")}",
            body => GitHubPullRequestCheckParser.Parse(body, revision, kind, page, account.AccessToken), cancellationToken);
    }

    public Task<PullRequestInspectionPage<PullRequestReview>> ReviewsAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, int page, CancellationToken cancellationToken)
        => ReadAsync(account, scope, revision, page, $"pulls/{revision.Number}/reviews",
            body => GitHubPullRequestReviewParser.Parse(body, account, scope, revision, page), cancellationToken);

    public Task<PullRequestInspectionPage<PullRequestActivity>> ActivityAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, int page, CancellationToken cancellationToken)
        => ReadAsync(account, scope, revision, page, $"issues/{revision.Number}/events",
            body => GitHubPullRequestActivityParser.Parse(body, account, scope, revision, page), cancellationToken);

    private async Task<PullRequestInspectionPage<T>> ReadAsync<T>(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, int page, string endpoint, Func<byte[], PullRequestInspectionPage<T>> parse,
        CancellationToken cancellationToken)
    {
        revision.Validate();
        if (page is < 1 or > 1000) throw new SourceReviewException("invalid_arguments");
        if (!account.IsEnabled || account.Id != scope.AccountId || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
        var body = await _transport.GetAsync(account, $"repos/{scope.Owner}/{scope.Repository}/{endpoint}?per_page=20&page={page}", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        try { return parse(body); }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }
}
