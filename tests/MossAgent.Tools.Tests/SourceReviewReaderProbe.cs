using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Tools.Tests;

internal sealed class SourceReviewReaderProbe : ISourceReviewReader
{
    public bool IsValid { get; set; } = true;
    public int Validations { get; private set; }
    public int Reads { get; private set; }
    public Func<CancellationToken, Task>? OnValidate { get; set; }
    public PullRequestSummary? Summary { get; set; }
    public async Task ValidateAsync(SourceRepositoryScope scope, CancellationToken cancellationToken)
    {
        Validations++;
        if (OnValidate is not null) await OnValidate(cancellationToken);
        if (!IsValid) throw new SourceReviewException("source_scope_denied");
    }
    public Task<PullRequestPage> ListAsync(SourceRepositoryScope scope, int page, CancellationToken cancellationToken)
    { Reads++; return Task.FromResult(new PullRequestPage([], page, false)); }
    public Task<PullRequestSummary> GetAsync(SourceRepositoryScope scope, int number, CancellationToken cancellationToken)
    { Reads++; return Task.FromResult(Summary ?? new PullRequestSummary(number, new string('a', 300), "author", "open", false, "body", "base", "head")); }
}
