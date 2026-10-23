using MossAgent.Application.Authorization;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AuthorizationResourceRegistrationGate(IAuthorizationResourceRegistry inner, bool pauseAfterRegistration)
    : IAuthorizationResourceRegistry
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task RegisterAsync(Guid leaseId, Guid projectId, long revision, CancellationToken cancellationToken)
    {
        if (pauseAfterRegistration) await inner.RegisterAsync(leaseId, projectId, revision, cancellationToken);
        Entered.TrySetResult();
        await Continue.Task.WaitAsync(cancellationToken);
        if (!pauseAfterRegistration) await inner.RegisterAsync(leaseId, projectId, revision, cancellationToken);
    }

    public Task HeartbeatAsync(Guid leaseId, CancellationToken cancellationToken) => inner.HeartbeatAsync(leaseId, cancellationToken);
    public Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken) => inner.ReleaseAsync(leaseId, cancellationToken);
    public Task<int> CountOutstandingAsync(Guid projectId, long maximumRevision, CancellationToken cancellationToken) =>
        inner.CountOutstandingAsync(projectId, maximumRevision, cancellationToken);
    public Task<IReadOnlyList<AuthorizationResourceLeaseSnapshot>> ListOutstandingAsync(Guid projectId, CancellationToken cancellationToken) =>
        inner.ListOutstandingAsync(projectId, cancellationToken);
    public Task<int> AbandonStaleAsync(Guid projectId, IReadOnlyCollection<Guid> leaseIds, DateTimeOffset staleBefore,
        CancellationToken cancellationToken) => inner.AbandonStaleAsync(projectId, leaseIds, staleBefore, cancellationToken);
}
