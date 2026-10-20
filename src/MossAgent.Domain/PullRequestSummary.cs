namespace MossAgent.Domain;

public sealed record PullRequestSummary(int Number, string Title, string Author, string State, bool IsDraft,
    string Body, string BaseRevision, string HeadRevision);
