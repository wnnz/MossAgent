namespace MossAgent.Domain;

public sealed record PullRequestActivity(long Id, string Event, string Actor, string? Detail,
    string? CommitRevision, DateTimeOffset? CreatedAt);
