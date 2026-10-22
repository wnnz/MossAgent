namespace MossAgent.Application.Git;

public sealed record GitReferenceSuggestion(
    string Name,
    string FullName,
    GitReferenceKind Kind,
    string Commit);
