namespace MossAgent.Domain;

public sealed record ProjectProfile(
    Guid Id,
    string Name,
    string PrimaryDirectory,
    IReadOnlyList<string> AuthorizedDirectories,
    DateTimeOffset CreatedAt);

