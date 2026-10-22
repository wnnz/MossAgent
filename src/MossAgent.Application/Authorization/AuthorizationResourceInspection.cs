namespace MossAgent.Application.Authorization;

public sealed record AuthorizationResourceInspection(
    IReadOnlyList<AuthorizationResourceLeaseSnapshot> Outstanding,
    IReadOnlyList<Guid> StaleLeaseIds)
{
    public static AuthorizationResourceInspection Empty { get; } = new([], []);
    public int OutstandingCount => Outstanding.Count;
    public int StaleCount => StaleLeaseIds.Count;
}
