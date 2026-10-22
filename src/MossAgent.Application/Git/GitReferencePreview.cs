namespace MossAgent.Application.Git;

public sealed record GitReferencePreview(
    string Reference,
    string Commit,
    string Subject,
    string Author,
    DateTimeOffset AuthoredAt);
