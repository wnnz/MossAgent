namespace MossAgent.Application.Git;

public sealed record GitWorktreeReference(
    string Name,
    string RefName,
    GitWorktreeReferenceKind Kind,
    string Commit,
    string Subject);
