using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Application.SourceControl;

public sealed class PullRequestCommentWriter(SourceRepositoryAccess access, IPullRequestClient summaries,
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
            return await ReconcileAsync(scope, revision.Number, body, cancellationToken);
        }
    }

    private static bool IsUnknownOutcome(SourceReviewException exception) =>
        exception.ErrorCode is "source_timeout" or "source_transport_failed" or "source_unavailable";

    private async Task<PullRequestCommentReceipt> ReconcileAsync(SourceRepositoryScope scope, int number,
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
}
