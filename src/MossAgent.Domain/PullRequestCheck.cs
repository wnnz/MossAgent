namespace MossAgent.Domain;

public sealed record PullRequestCheck(long Id, string Name, string Status, string? Conclusion, string Kind);
