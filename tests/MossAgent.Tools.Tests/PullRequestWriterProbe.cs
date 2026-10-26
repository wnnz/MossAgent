using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Tools.Tests;

internal sealed class PullRequestWriterProbe : ISourcePullRequestWriter
{
    public bool IsValid { get; set; } = true;
    public int Validations { get; private set; }
    public int Writes { get; private set; }
    public int Reviews { get; private set; }
    public int Closes { get; private set; }
    public int Merges { get; private set; }
    public PullRequestCommentReceipt Receipt { get; set; } = new(11, false);
    public PullRequestReviewReceipt ReviewReceipt { get; set; } = new(31, false);
    public PullRequestCloseReceipt CloseReceipt { get; set; } = new("closed", false);
    public PullRequestMergeReceipt MergeReceipt { get; set; } = new(new string('c', 40), false);
    public string? FailureCode { get; set; }
    public string? ReviewFailureCode { get; set; }
    public string? CloseFailureCode { get; set; }
    public string? MergeFailureCode { get; set; }
    public Func<CancellationToken, Task>? OnWrite { get; set; }
    public Func<CancellationToken, Task>? OnReview { get; set; }
    public Func<CancellationToken, Task>? OnClose { get; set; }
    public Func<CancellationToken, Task>? OnMerge { get; set; }
    public PullRequestReviewDecision? LastDecision { get; private set; }
    public PullRequestMergeMethod? LastMethod { get; private set; }
    public string? LastBody { get; private set; }
    public bool BodyWasSent { get; private set; }

    public Task ValidateWriteAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken)
    {
        Validations++;
        revision.Validate();
        if (!IsValid || !scope.CanWrite) throw new SourceReviewException("source_scope_denied");
        return Task.CompletedTask;
    }

    public async Task<PullRequestCommentReceipt> CommentAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken)
    {
        Writes++;
        revision.Validate();
        if (!scope.CanWrite || string.IsNullOrEmpty(body) || body.Length > 4096) throw new SourceReviewException("source_scope_denied");
        if (OnWrite is not null) await OnWrite(cancellationToken);
        if (FailureCode is not null) throw new SourceReviewException(FailureCode);
        return Receipt;
    }

    public async Task<PullRequestReviewReceipt> ReviewAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken)
    {
        Reviews++;
        revision.Validate();
        LastDecision = decision;
        LastBody = body;
        BodyWasSent = body is not null;
        if (!scope.CanWrite || body is { Length: > 4096 }
            || decision is not (PullRequestReviewDecision.Approve or PullRequestReviewDecision.RequestChanges
                or PullRequestReviewDecision.Comment))
            throw new SourceReviewException("source_scope_denied");
        if (OnReview is not null) await OnReview(cancellationToken);
        if (ReviewFailureCode is not null) throw new SourceReviewException(ReviewFailureCode);
        return ReviewReceipt;
    }

    public async Task<PullRequestCloseReceipt> CloseAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken)
    {
        Closes++;
        revision.Validate();
        if (!scope.CanWrite) throw new SourceReviewException("source_scope_denied");
        if (OnClose is not null) await OnClose(cancellationToken);
        if (CloseFailureCode is not null) throw new SourceReviewException(CloseFailureCode);
        return CloseReceipt;
    }

    public async Task<PullRequestMergeReceipt> MergeAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestMergeMethod method, CancellationToken cancellationToken)
    {
        Merges++;
        revision.Validate();
        LastMethod = method;
        if (!scope.CanWrite || method is not (PullRequestMergeMethod.Merge or PullRequestMergeMethod.Squash
            or PullRequestMergeMethod.Rebase)) throw new SourceReviewException("source_scope_denied");
        if (OnMerge is not null) await OnMerge(cancellationToken);
        if (MergeFailureCode is not null) throw new SourceReviewException(MergeFailureCode);
        return MergeReceipt;
    }
}
