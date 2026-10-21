namespace MossAgent.Domain;

public sealed record ProjectProfile(
    Guid Id,
    string Name,
    string PrimaryDirectory,
    IReadOnlyList<string> AuthorizedDirectories,
    DateTimeOffset CreatedAt)
{
    public long AuthorizationRevision { get; init; } = 1;
    public IReadOnlyList<ProjectRootAuthorization> RootAuthorizations { get; init; } = [];
}

