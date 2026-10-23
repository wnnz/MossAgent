namespace MossAgent.Application.Git;

public sealed record GitWorktreeReferencePage(
    IReadOnlyList<GitWorktreeReference> Items,
    int NextSkip,
    bool HasMore,
    bool Truncated);
