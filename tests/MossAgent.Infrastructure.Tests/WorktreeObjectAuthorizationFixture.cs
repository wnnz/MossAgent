using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class WorktreeObjectAuthorizationFixture : IAsyncDisposable
{
    private WorktreeObjectAuthorizationFixture(GitWorktreeSecurityFixture git, SqliteFailurePersistenceFixture storage)
    {
        Git = git;
        Storage = storage;
        Service = Reopen();
    }
    public GitWorktreeSecurityFixture Git { get; }
    public SqliteFailurePersistenceFixture Storage { get; }
    public RootIdentityProbe Roots { get; } = new();
    public GitWorktreeService Service { get; }
    public ProjectProfile Project => Git.Project;
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public GitWorktreeService Reopen() => new(Git.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()),
        roots: Roots, connections: Storage.Connections);

    public static async Task<WorktreeObjectAuthorizationFixture> CreateAsync()
    {
        var git = await GitWorktreeSecurityFixture.CreateAsync();
        try
        {
            var storage = await SqliteFailurePersistenceFixture.CreateAsync();
            try
            {
                await storage.Repository.SaveProjectAsync(git.Project, storage.Token);
                return new(git, storage);
            }
            catch { await storage.DisposeAsync(); throw; }
        }
        catch { git.Dispose(); throw; }
    }

    public async Task<AgentTask> SaveTaskAsync(Guid id, string path)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(id, Project.Id, "worktree object fixture", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Interrupted, now, now, path);
        await Storage.Repository.SaveTaskAsync(task, Token);
        return task;
    }

    public async ValueTask DisposeAsync()
    {
        await Storage.DisposeAsync();
        Git.Dispose();
    }
}
