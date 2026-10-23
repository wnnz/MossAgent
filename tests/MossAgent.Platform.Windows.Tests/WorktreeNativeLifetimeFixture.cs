using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Files;
using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class WorktreeNativeLifetimeFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private SqliteConnectionFactory? _connections;
    private readonly GitProcessRunner _git = new(new FixtureTaskProcessLauncher());
    public WorktreeNativeRootFactory Roots { get; } = new();
    public ProjectProfile Project { get; private set; } = null!;
    public SqliteWorkspaceRepository Repository { get; private set; } = null!;
    public ProjectAuthorizationMonitor Monitor { get; private set; } = null!;
    public ProjectAuthorizationMonitor Peer { get; private set; } = null!;
    public SqliteAuthorizationResourceRegistry PeerResources { get; private set; } = null!;
    public GitWorktreeService Service { get; private set; } = null!;
    public SqliteWorktreeCleanupIntentStore Cleanups { get; private set; } = null!;
    public CancellationToken Token => TestContext.Current.CancellationToken;

    public static async Task<WorktreeNativeLifetimeFixture> CreateAsync()
    {
        var fixture = new WorktreeNativeLifetimeFixture();
        try { await fixture.InitializeAsync(); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync()
    {
        var repository = Path.Combine(_root, "repository");
        Directory.CreateDirectory(repository);
        var paths = new AppDataPaths(Path.Combine(_root, "app-data"));
        var database = _connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, database).InitializeAsync(Token);
        var changes = new ProjectAuthorizationChanges();
        Repository = new(database, authorizationChanges: changes, rootIdentities: Roots);
        Project = new(Guid.NewGuid(), "native worktree lifetime", repository, [repository], DateTimeOffset.UtcNow);
        await Repository.SaveProjectAsync(Project, Token);
        Project = await Repository.ConfirmRootObjectsAsync(Project, Token);
        Monitor = new(Repository, changes, roots: Roots,
            resources: new SqliteAuthorizationResourceRegistry(database, TimeProvider.System));
        PeerResources = new(database, TimeProvider.System);
        Peer = new(Repository, new ProjectAuthorizationChanges(), resources: PeerResources);
        Cleanups = new(database, TimeProvider.System);
        Service = new(paths, _git, roots: Roots, connections: database, authorizations: Monitor,
            exclusiveDirectories: new WindowsExclusiveDirectoryClaimFactory(),
            deleteSessions: new WindowsPathDeleteSessionFactory(), cleanupIntents: Cleanups,
            authorizationChanges: changes);
        await GitAsync("init", "-b", "main");
        await GitAsync("config", "user.name", "MossAgent fixture");
        await GitAsync("config", "user.email", "fixture@example.invalid");
        await GitAsync("config", "core.hooksPath", Path.Combine(_root, "empty-hooks"));
        await GitAsync("commit", "--allow-empty", "-m", "initial");
    }

    private async Task GitAsync(params string[] arguments)
    {
        var result = await _git.RunAsync(Project.PrimaryDirectory, arguments, Token);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.OutputTruncated);
    }

    public async Task<AgentTask> CreateWorktreeAsync()
    {
        var id = Guid.NewGuid();
        var path = await Service.CreateAsync(Project, id, Token);
        var task = new AgentTask(id, Project.Id, "native lifetime", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Interrupted, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, path);
        await Repository.SaveTaskAsync(task, Token);
        return task;
    }

    public Task<ProjectAuthorizationDrainResult> DrainAsync(bool peer = false) =>
        (peer ? Peer : Monitor).WaitForQuiescenceAsync(Project.Id, Project.AuthorizationRevision,
            TimeSpan.FromMilliseconds(100), Token);

    public async Task WaitForDrainAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!(await DrainAsync()).IsDrained || !(await DrainAsync(peer: true)).IsDrained)
            await Task.Delay(15, timeout.Token);
        Assert.Empty(await PeerResources.ListOutstandingAsync(Project.Id, Token));
    }

    public async Task AssertCleanupCompletedAsync(Guid taskId)
    {
        await using var connection = _connections!.Create();
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT state,quarantine_path FROM worktree_cleanup_intents WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Token);
        Assert.True(await reader.ReadAsync(Token));
        Assert.Equal((long)WorktreeCleanupState.Completed, reader.GetInt64(0));
        Assert.False(Directory.Exists(reader.GetString(1)));
        Assert.False(await reader.ReadAsync(Token));
        var remaining = Assert.Single(await Cleanups.ReadIncompleteAsync(Project.Id, Token));
        Assert.Equal(taskId, remaining.TaskId);
        Assert.Equal(WorktreeCleanupState.Completed, remaining.State);
    }

    public async ValueTask DisposeAsync()
    {
        Roots.Release();
        if (Monitor is not null) await Monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), Token);
        if (Peer is not null) await Peer.DisposeAsync();
        if (_connections is not null)
        {
            using var pool = _connections.Create();
            Microsoft.Data.Sqlite.SqliteConnection.ClearPool(pool);
        }
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        await WindowsProcessTestDirectory.DeleteAsync(_root);
    }
}
