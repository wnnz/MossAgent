namespace MossAgent.Domain;

public sealed record PullRequestInboxPage(IReadOnlyList<PullRequestInboxItem> Items, int Page,
    bool HasNextPage, long TotalCount, bool IsIncomplete);
