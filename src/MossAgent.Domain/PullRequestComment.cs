namespace MossAgent.Domain;

public sealed record PullRequestComment(long Id, string Kind, string Author, string Body,
    string? Path, int? Line, string? Side, string? CommitRevision, int? StartLine = null, string? StartSide = null);
