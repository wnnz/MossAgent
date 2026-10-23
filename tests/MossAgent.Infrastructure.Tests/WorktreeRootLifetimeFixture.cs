using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class WorktreeRootLifetimeFixture : IAsyncDisposable
{
    private readonly WorktreeObjectAuthorizationFixture _inner;
    private readonly TaskCompletionSource<IRootIdentitySession> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DirectoryObjectIdentity? _blockedIdentity;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CleanupAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ProjectAuthorizationChanges Changes { get; } = new();
    public ProjectProfile Project { get; private set; } = null!;
    public ProjectAuthorizationMonitor Monitor { get; private set; } = null!;
    public ProjectAuthorizationMonitor Peer { get; private set; } = null!;
    public SqliteAuthorizationResourceRegistry PeerResources { get; private set; } = null!;
    public SqliteWorkspaceRepository Repository { get; private set; } = null!;
    public GitWorktreeService Service { get; private set; } = null!;
    public CancellationToken Token => _inner.Token;
    public GitWorktreeSecurityFixture Git => _inner.Git;

    private WorktreeRootLifetimeFixture(WorktreeObjectAuthorizationFixture inner) => _inner = inner;

    public static async Task<WorktreeRootLifetimeFixture> CreateAsync(bool monitorRoots = true, TimeProvider? operationTime = null)
    {
        var fixture = new WorktreeRootLifetimeFixture(await WorktreeObjectAuthorizationFixture.CreateAsync());
        try
        {
            var database = fixture._inner.Storage.Connections;
            fixture.Repository = new(database, authorizationChanges: fixture.Changes, rootIdentities: fixture._inner.Roots);
            fixture.Project = await fixture.Repository.ConfirmRootObjectsAsync(fixture._inner.Project, fixture.Token);
            fixture.Monitor = new(fixture.Repository, fixture.Changes, roots: monitorRoots ? fixture._inner.Roots : null,
                resources: new SqliteAuthorizationResourceRegistry(database, TimeProvider.System));
            fixture.PeerResources = new(database, TimeProvider.System);
            fixture.Peer = new(fixture.Repository, new ProjectAuthorizationChanges(), resources: fixture.PeerResources);
            var removal = new TestGitWorktreeRemovalFactory();
            fixture.Service = new(fixture.Git.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()), operationTime,
                roots: fixture._inner.Roots, connections: database, authorizations: fixture.Monitor,
                quarantineSessions: removal, deleteSessions: removal,
                cleanupIntents: new InMemoryWorktreeCleanupIntentService(), authorizationChanges: fixture.Changes);
            return fixture;
        }
        catch { await fixture._inner.DisposeAsync(); throw; }
    }

    public async Task<AgentTask> PrepareAsync(string operation)
    {
        var id = Guid.NewGuid();
        var path = Git.Target(id);
        if (operation == "confirm") await Git.Service.CreateAsync(Project, id, Token);
        else if (operation != "create") await _inner.Service.CreateAsync(Project, id, Token);
        var task = new AgentTask(id, Project.Id, "root lifetime fixture", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Interrupted, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, path);
        if (operation != "create") await Repository.SaveTaskAsync(task, Token);
        return task;
    }

    public async Task BlockWorktreeAsync(string path, int occurrence = 1)
    {
        using var snapshot = await _inner.Roots.OpenAsync([Project.PrimaryDirectory, path, Git.Outside], Token);
        var identities = snapshot.Roots.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        _blockedIdentity = identities[path];
        var count = 0;
        _inner.Roots.Override = (requested, _) =>
        {
            if (requested.Count == 1 && string.Equals(requested[0], path, StringComparison.OrdinalIgnoreCase)
                && Interlocked.Increment(ref count) == occurrence)
            {
                Entered.TrySetResult();
                return _pending.Task;
            }
            return Task.FromResult<IRootIdentitySession>(new RootIdentityProbeSession(
                requested.Select(root => identities[root]).ToArray(), () => { }));
        };
    }

    public async Task RunAsync(string operation, AgentTask task, CancellationToken token)
    {
        switch (operation)
        {
            case "acquire": await using (await Service.AcquireAsync(Project, task.Id, task.WorktreePath!, token)) { } break;
            case "validate": await Service.ValidateAsync(Project, task.Id, task.WorktreePath!, token); break;
            case "confirm": await Service.ConfirmLegacyAsync(Project, task, token); break;
            case "create": await Service.CreateAsync(Project, task.Id, token); break;
            case "reauthorize": await Service.ReauthorizeAsync(Project, task, token); break;
            case "remove": case "remove-transfer": await Service.RemoveAsync(Project, task.WorktreePath!, token); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public void CompleteNative(bool cleanupFails = false)
    {
        if (_blockedIdentity is null) return;
        _pending.TrySetResult(new RootIdentityProbeSession([_blockedIdentity], () =>
        {
            CleanupAttempted.TrySetResult();
            if (cleanupFails) throw new InvalidOperationException("Synthetic worktree root cleanup failure.");
        }));
    }

    public void FailNative() => _pending.TrySetException(new IOException("Synthetic worktree root open failure."));

    public Task<ProjectAuthorizationDrainResult> DrainAsync(bool peer = false) =>
        (peer ? Peer : Monitor).WaitForQuiescenceAsync(Project.Id, Project.AuthorizationRevision,
            TimeSpan.FromMilliseconds(75), Token);

    public async Task WaitForDrainAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!(await DrainAsync()).IsDrained || !(await DrainAsync(peer: true)).IsDrained)
            await Task.Delay(15, timeout.Token);
        Assert.Empty(await PeerResources.ListOutstandingAsync(Project.Id, Token));
    }

    public async ValueTask DisposeAsync()
    {
        CompleteNative();
        if (Entered.Task.IsCompleted && !_pending.Task.IsFaulted)
            await CleanupAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        try { await Monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token); }
        catch (InvalidOperationException) { }
        await Peer.DisposeAsync();
        await _inner.DisposeAsync();
    }
}
