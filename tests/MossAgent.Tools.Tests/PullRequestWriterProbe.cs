using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Tools.Tests;

internal sealed class PullRequestWriterProbe : ISourcePullRequestWriter
{
    public bool IsValid { get; set; } = true;
    public int Validations { get; private set; }
    public int Writes { get; private set; }
    public PullRequestCommentReceipt Receipt { get; set; } = new(11, false);
    public string? FailureCode { get; set; }
    public Func<CancellationToken, Task>? OnWrite { get; set; }

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
}
