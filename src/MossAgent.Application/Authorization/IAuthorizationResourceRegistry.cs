namespace MossAgent.Application.Authorization;

public interface IAuthorizationResourceRegistry
{
    Task RegisterAsync(Guid leaseId, Guid projectId, long revision, CancellationToken cancellationToken);
    Task HeartbeatAsync(Guid leaseId, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken);
    Task<int> CountOutstandingAsync(Guid projectId, long maximumRevision, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuthorizationResourceLeaseSnapshot>> ListOutstandingAsync(
        Guid projectId, CancellationToken cancellationToken);
    Task<int> AbandonStaleAsync(Guid projectId, IReadOnlyCollection<Guid> leaseIds,
        DateTimeOffset staleBefore, CancellationToken cancellationToken);
}
