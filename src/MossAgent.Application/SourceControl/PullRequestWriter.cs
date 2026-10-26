using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Application.SourceControl;

public sealed class PullRequestWriter(SourceRepositoryAccess access, IPullRequestClient summaries,
    IPullRequestWriteClient client) : ISourcePullRequestWriter
{
    public async Task ValidateWriteAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken)
    {
        revision.Validate();
        _ = await access.ResolveWriteAsync(scope, cancellationToken);
    }

    // The write grant is resolved before and after the live revision check so a revocation
    // landing between the two lookups still fails closed. The remote write is never retried:
    // when its outcome is unknown a single read reconciles by exact body match.
    public async Task<PullRequestCommentReceipt> CommentAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken)
    {
        revision.Validate();
        if (string.IsNullOrEmpty(body) || body.Length > 4096) throw new SourceReviewException("invalid_arguments");
        var account = await access.ResolveWriteAsync(scope, cancellationToken);
        var current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        if (!revision.Matches(current)) throw new SourceReviewException("source_revision_changed");
        account = await access.ResolveWriteAsync(scope, cancellationToken);
        try
        {
            return new(await client.CommentAsync(account, scope, revision, body, cancellationToken), Reconciled: false);
        }
        catch (SourceReviewException exception) when (IsUnknownOutcome(exception))
        {
            return await ReconcileCommentAsync(scope, revision.Number, body, cancellationToken);
        }
    }

    // Reviews share the comment ordering: resolve grant, verify the live revision, re-resolve,
    // then a single POST pinned to the captured head via commit_id. An unknown outcome reconciles
    // on the recent reviews list matched by state, head commit and normalized body; never re-POST.
    public async Task<PullRequestReviewReceipt> ReviewAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken)
    {
        revision.Validate();
        if (decision is not (PullRequestReviewDecision.Approve or PullRequestReviewDecision.RequestChanges
            or PullRequestReviewDecision.Comment) || body is { Length: > 4096 })
            throw new SourceReviewException("invalid_arguments");
        var account = await access.ResolveWriteAsync(scope, cancellationToken);
        var current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        if (!revision.Matches(current)) throw new SourceReviewException("source_revision_changed");
        account = await access.ResolveWriteAsync(scope, cancellationToken);
        try
        {
            return new(await client.SubmitReviewAsync(account, scope, revision, decision, body, cancellationToken),
                Reconciled: false);
        }
        catch (SourceReviewException exception) when (IsUnknownOutcome(exception))
        {
            return await ReconcileReviewAsync(scope, revision, decision, body, cancellationToken);
        }
    }

    private static bool IsUnknownOutcome(SourceReviewException exception) =>
        exception.ErrorCode is "source_timeout" or "source_transport_failed" or "source_unavailable";

    private async Task<PullRequestCommentReceipt> ReconcileCommentAsync(SourceRepositoryScope scope, int number,
        string body, CancellationToken cancellationToken)
    {
        long? found;
        try
        {
            var account = await access.ResolveWriteAsync(scope, cancellationToken);
            found = await client.FindCommentAsync(account, scope, number, body, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_write_unknown"); }
        return found is { } id ? new(id, Reconciled: true) : throw new SourceReviewException("source_write_unconfirmed");
    }

    private async Task<PullRequestReviewReceipt> ReconcileReviewAsync(SourceRepositoryScope scope,
        PullRequestRevision revision, PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken)
    {
        long? found;
        try
        {
            var account = await access.ResolveWriteAsync(scope, cancellationToken);
            found = await client.FindReviewAsync(account, scope, revision, decision, body, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_write_unknown"); }
        return found is { } id ? new(id, Reconciled: true) : throw new SourceReviewException("source_write_unconfirmed");
    }
}
