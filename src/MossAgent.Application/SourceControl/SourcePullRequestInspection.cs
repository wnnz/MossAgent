using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Application.SourceControl;

public sealed class SourcePullRequestInspection(SourceRepositoryAccess access, IPullRequestClient summaries,
    IPullRequestInspectionClient inspection) : ISourcePullRequestInspection
{
    public async Task ValidateAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken)
    {
        revision.Validate();
        _ = await access.ResolveAsync(scope, cancellationToken);
    }

    public Task<PullRequestInspectionPage<PullRequestFile>> FilesAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        int page, CancellationToken cancellationToken) => ReadAsync(scope, revision, page,
            account => inspection.FilesAsync(account, scope, revision, page, cancellationToken), cancellationToken);

    public Task<PullRequestInspectionPage<PullRequestComment>> CommentsAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        string kind, int page, CancellationToken cancellationToken) => ReadAsync(scope, revision, page,
            account => inspection.CommentsAsync(account, scope, revision, kind, page, cancellationToken), cancellationToken);

    public Task<PullRequestInspectionPage<PullRequestCheck>> ChecksAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        string kind, int page, CancellationToken cancellationToken) => ReadAsync(scope, revision, page,
            account => inspection.ChecksAsync(account, scope, revision, kind, page, cancellationToken), cancellationToken);

    private async Task<PullRequestInspectionPage<T>> ReadAsync<T>(SourceRepositoryScope scope, PullRequestRevision revision,
        int page, Func<SourceAccount, Task<PullRequestInspectionPage<T>>> read, CancellationToken cancellationToken)
    {
        revision.Validate();
        if (page is < 1 or > 1000) throw new SourceReviewException("invalid_arguments");
        var account = await access.ResolveAsync(scope, cancellationToken);
        await CheckRevisionAsync(account, scope, revision, cancellationToken);
        _ = await access.ResolveAsync(scope, cancellationToken);
        var result = await read(account);
        _ = await access.ResolveAsync(scope, cancellationToken);
        await CheckRevisionAsync(account, scope, revision, cancellationToken);
        _ = await access.ResolveAsync(scope, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.Revision != revision) throw new SourceReviewException("source_revision_changed");
        return result;
    }

    private async Task CheckRevisionAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken)
    {
        var current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        if (!revision.Matches(current)) throw new SourceReviewException("source_revision_changed");
    }
}
