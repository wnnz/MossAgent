using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

/// <summary>Owns native root operations under one registered project authorization lease.</summary>
public sealed class ProjectRootOperationScope : ITaskWorkspaceLease
{
    private readonly ProjectAuthorizationLease _lease;

    internal ProjectRootOperationScope(ProjectAuthorizationLease lease) => _lease = lease;

    public Guid ProjectId => _lease.ProjectId;
    public long Revision => _lease.Revision;
    public CancellationToken Revocation => _lease.Revocation;
    public bool IsRevoked => _lease.IsRevoked;

    public IRootIdentitySessionFactory TrackRoots(IRootIdentitySessionFactory factory) => _lease.TrackRoots(factory);

    /// <summary>
    /// Stops new opens without waiting for a cancellation-ignoring native open. The monitor
    /// retains ownership and the persistent reservation until actual cleanup succeeds.
    /// </summary>
    public Task DisposeAfterOperationAsync() => _lease.DisposeAfterValidationAsync();

    public ValueTask DisposeAsync() => _lease.DisposeAsync();
}
