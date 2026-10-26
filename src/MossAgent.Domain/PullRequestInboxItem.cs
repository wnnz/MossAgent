namespace MossAgent.Domain;

public sealed record PullRequestInboxItem(long Id, int Number, string Title, string Author,
    string Owner, string Repository, string State, bool IsDraft, DateTimeOffset? UpdatedAt);
