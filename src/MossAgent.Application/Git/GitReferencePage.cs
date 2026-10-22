namespace MossAgent.Application.Git;

public sealed record GitReferencePage(
    IReadOnlyList<GitReferenceSuggestion> Items,
    int Page,
    int PageSize,
    bool HasMore);
