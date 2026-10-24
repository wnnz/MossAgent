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

    /// <summary>Returns a defensive copy whose mutable list members are detached
    /// from the caller's lists before the profile crosses a thread boundary.</summary>
    public ProjectProfile Snapshot() => this with
    {
        AuthorizedDirectories = AuthorizedDirectories.ToArray(),
        RootAuthorizations = RootAuthorizations.ToArray(),
    };
}

