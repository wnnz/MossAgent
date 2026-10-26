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

    // Close shares the comment/review ordering — resolve grant, verify the live revision,
    // re-resolve, then a single PATCH. The difference is the reconcile evidence: closing has
    // no created resource to find, so a single summary read is authoritative on state. An
    // unknown outcome reconciles on state == "closed" under the same revision; never re-PATCH.
    public async Task<PullRequestCloseReceipt> CloseAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken)
    {
        revision.Validate();
        var account = await access.ResolveWriteAsync(scope, cancellationToken);
        var current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        if (!revision.Matches(current)) throw new SourceReviewException("source_revision_changed");
        account = await access.ResolveWriteAsync(scope, cancellationToken);
        try
        {
            var state = await client.CloseAsync(account, scope, revision, cancellationToken);
            if (!"closed".Equals(state, StringComparison.OrdinalIgnoreCase))
                throw new SourceReviewException("source_invalid_response");
            return new("closed", Reconciled: false);
        }
        catch (SourceReviewException exception) when (IsUnknownOutcome(exception))
        {
            return await ReconcileCloseAsync(scope, revision, cancellationToken);
        }
    }

    // Merge shares the close ordering — resolve grant, verify the live revision,
    // re-resolve, then a single PUT pinned to the captured head via the sha field.
    // The merge itself advances the base revision, so reconcile evidence is
    // merged == true under the same head, never the summary match; never re-PUT.
    public async Task<PullRequestMergeReceipt> MergeAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestMergeMethod method, CancellationToken cancellationToken)
    {
        revision.Validate();
        if (method is not (PullRequestMergeMethod.Merge or PullRequestMergeMethod.Squash
            or PullRequestMergeMethod.Rebase))
            throw new SourceReviewException("invalid_arguments");
        var account = await access.ResolveWriteAsync(scope, cancellationToken);
        var current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        if (!revision.Matches(current)) throw new SourceReviewException("source_revision_changed");
        account = await access.ResolveWriteAsync(scope, cancellationToken);
        try
        {
            return new(await client.MergeAsync(account, scope, revision, method, cancellationToken), Reconciled: false);
        }
        catch (SourceReviewException exception) when (IsUnknownOutcome(exception))
        {
            return await ReconcileMergeAsync(scope, revision, cancellationToken);
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

    private async Task<PullRequestCloseReceipt> ReconcileCloseAsync(SourceRepositoryScope scope,
        PullRequestRevision revision, CancellationToken cancellationToken)
    {
        PullRequestSummary current;
        try
        {
            var account = await access.ResolveWriteAsync(scope, cancellationToken);
            current = await summaries.GetAsync(account, scope, revision.Number, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_write_unknown"); }
        return revision.Matches(current) && current.State.Equals("closed", StringComparison.OrdinalIgnoreCase)
            ? new("closed", Reconciled: true) : throw new SourceReviewException("source_write_unconfirmed");
    }

    private async Task<PullRequestMergeReceipt> ReconcileMergeAsync(SourceRepositoryScope scope,
        PullRequestRevision revision, CancellationToken cancellationToken)
    {
        string? sha;
        try
        {
            var account = await access.ResolveWriteAsync(scope, cancellationToken);
            sha = await client.MergedAsync(account, scope, revision, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_write_unknown"); }
        return sha is not null ? new(sha, Reconciled: true)
            : throw new SourceReviewException("source_write_unconfirmed");
    }
}
