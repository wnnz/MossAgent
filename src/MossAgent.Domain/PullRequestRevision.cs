namespace MossAgent.Domain;

public sealed record PullRequestRevision(int Number, string BaseRevision, string HeadRevision)
{
    public void Validate()
    {
        if (Number <= 0 || !IsCommit(BaseRevision) || !IsCommit(HeadRevision))
            throw new SourceReviewException("invalid_arguments");
    }

    public static bool IsCommit(string? value) => value is { Length: 40 or 64 }
        && value.All(char.IsAsciiHexDigit);

    public bool Matches(PullRequestSummary summary) => Number == summary.Number
        && BaseRevision.Equals(summary.BaseRevision, StringComparison.OrdinalIgnoreCase)
        && HeadRevision.Equals(summary.HeadRevision, StringComparison.OrdinalIgnoreCase);
}
