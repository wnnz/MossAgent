using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class PendingAuthorizationRootFixture : IAsyncDisposable
{
    private readonly SqliteFailurePersistenceFixture _database;
    private readonly RootIdentityProbe _roots;
    private readonly TaskCompletionSource<IRootIdentitySession> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IReadOnlyList<DirectoryObjectIdentity> _identities;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken Token => _database.Token;
    public ProjectProfile Project { get; }
    public ProjectAuthorizationMonitor Monitor { get; }
    public ProjectAuthorizationMonitor Peer { get; }
    public SqliteAuthorizationResourceRegistry PeerResources { get; }

    private PendingAuthorizationRootFixture(SqliteFailurePersistenceFixture database, RootIdentityProbe roots,
        ProjectProfile project, IReadOnlyList<DirectoryObjectIdentity> identities, bool persistent)
    {
        _database = database;
        _roots = roots;
        _identities = identities;
        Project = project;
        PeerResources = new(database.Connections, TimeProvider.System);
        Monitor = new(database.Repository, new ProjectAuthorizationChanges(), roots: roots,
            resources: persistent ? new SqliteAuthorizationResourceRegistry(database.Connections, TimeProvider.System) : null);
        Peer = new(database.Repository, new ProjectAuthorizationChanges(), resources: PeerResources);
    }

    public static async Task<PendingAuthorizationRootFixture> CreateAsync(bool persistent = true)
    {
        var database = await SqliteFailurePersistenceFixture.CreateAsync();
        try
        {
            var roots = new RootIdentityProbe();
            var repository = new SqliteWorkspaceRepository(database.Connections, rootIdentities: roots);
            var project = await repository.ConfirmRootObjectsAsync(database.Project, database.Token);
            using var snapshot = await roots.OpenAsync(project.AuthorizedDirectories, database.Token);
            return new(database, roots, project, snapshot.Roots.ToArray(), persistent);
        }
        catch { await database.DisposeAsync(); throw; }
    }

    public void BlockRoots() => _roots.Override = (_, _) => { Entered.TrySetResult(); return _pending.Task; };

    public async Task ValidateOrAcquireAsync(bool acquire, CancellationToken token)
    {
        if (acquire) { await using var lease = await Monitor.AcquireAsync(Project, token); }
        else await Monitor.ValidateAsync(Project, token);
    }

    public void UseReader(Func<IReadOnlyList<string>, CancellationToken, Task<IRootIdentitySession>> reader) =>
        _roots.Override = reader;

    public IRootIdentitySession NativeSession(Action released) => new RootIdentityProbeSession(_identities, released);

    public void CompleteNative(bool cleanupFails = false) => _pending.TrySetResult(
        new RootIdentityProbeSession(_identities, () =>
        {
            ReleaseAttempted.TrySetResult();
            if (cleanupFails) throw new InvalidOperationException("Synthetic late root cleanup failure.");
        }));

    public void FailNative() => _pending.TrySetException(new InvalidOperationException("Synthetic native open failure."));

    public Task<long> CountRegistrationsAsync() => _database.CountAsync("SELECT COUNT(*) FROM authorization_resource_leases;");

    public Task<ProjectAuthorizationDrainResult> DrainAsync(bool peer = false) =>
        (peer ? Peer : Monitor).WaitForQuiescenceAsync(Project.Id, Project.AuthorizationRevision,
            TimeSpan.FromMilliseconds(75), Token);

    public async ValueTask DisposeAsync()
    {
        CompleteNative();
        if (Entered.Task.IsCompleted && !_pending.Task.IsFaulted)
            await ReleaseAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        try { await Monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token); }
        catch (InvalidOperationException) { }
        await Peer.DisposeAsync();
        await _database.DisposeAsync();
    }
}
