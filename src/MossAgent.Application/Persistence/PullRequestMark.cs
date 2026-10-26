namespace MossAgent.Application.Persistence;

public sealed record PullRequestMark(Guid AccountId, string Owner, string Repository,
    int Number, string HeadRevision, DateTimeOffset ViewedAt);
