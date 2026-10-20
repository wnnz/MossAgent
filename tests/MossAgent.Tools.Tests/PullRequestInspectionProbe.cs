using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Tools.Tests;

internal sealed class PullRequestInspectionProbe : ISourcePullRequestInspection
{
    public bool IsValid { get; set; } = true;
    public int Validations { get; private set; }
    public int Reads { get; private set; }
    public Func<CancellationToken, Task>? OnRead { get; set; }
    public Task ValidateAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken)
    {
        Validations++; revision.Validate();
        if (!IsValid) throw new SourceReviewException("source_scope_denied");
        return Task.CompletedTask;
    }
    private async Task<PullRequestInspectionPage<T>> PageAsync<T>(PullRequestRevision revision, int page, CancellationToken token)
    {
        Reads++;
        if (OnRead is not null) await OnRead(token);
        return new(revision, [], page, false);
    }
    public Task<PullRequestInspectionPage<PullRequestFile>> FilesAsync(SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken) => PageAsync<PullRequestFile>(revision, page, cancellationToken);
    public Task<PullRequestInspectionPage<PullRequestComment>> CommentsAsync(SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken) => PageAsync<PullRequestComment>(revision, page, cancellationToken);
    public Task<PullRequestInspectionPage<PullRequestCheck>> ChecksAsync(SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken) => PageAsync<PullRequestCheck>(revision, page, cancellationToken);
}
