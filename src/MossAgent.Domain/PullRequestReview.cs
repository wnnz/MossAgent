namespace MossAgent.Domain;

public sealed record PullRequestReview(long Id, string Author, string State, string Body,
    string? CommitRevision, DateTimeOffset? SubmittedAt);
