namespace MossAgent.Domain;

public sealed record PullRequestInspectionPage<T>(PullRequestRevision Revision, IReadOnlyList<T> Items,
    int Page, bool HasNextPage, int? TotalCount = null, bool IsLimited = false);
