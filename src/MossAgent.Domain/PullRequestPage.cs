namespace MossAgent.Domain;

public sealed record PullRequestPage(IReadOnlyList<PullRequestSummary> Items, int Page, bool HasNextPage);
