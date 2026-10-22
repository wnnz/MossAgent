using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteWorktreeCreationIntentStoreTests
{
    [Fact]
    public async Task IntentLifecycleOnlyLeavesCrossProcessEvidenceUntilConsumed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteWorktreeCreationIntentStore(fixture.Connections, TimeProvider.System);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Project.PrimaryDirectory, "worktrees", taskId.ToString("N"));
        await store.BeginAsync(fixture.Project.Id, taskId, fixture.Project.PrimaryDirectory,
            "0123456789abcdef", target, fixture.Token);
        Assert.Equal(WorktreeCreationStatus.Preparing,
            Assert.Single(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token)).Status);

        await store.MarkClaimedAsync(taskId, target,
            new DirectoryObjectIdentity(target, "volume", "object"), fixture.Token);
        var claimed = Assert.Single(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(WorktreeCreationStatus.Claimed, claimed.Status);
        Assert.Equal("object", claimed.ObjectId);

        await store.MarkGitCreatedAsync(taskId, target, fixture.Token);
        Assert.Equal(WorktreeCreationStatus.GitCreated,
            Assert.Single(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token)).Status);

        await store.MarkConsumedAsync(taskId, target, fixture.Token);
        Assert.Empty(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM worktree_creation_intents WHERE status=4;"));
    }

    [Fact]
    public async Task StartupPreservesUnconsumedCreatedIntentAsOrphaned()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteWorktreeCreationIntentStore(fixture.Connections, TimeProvider.System);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Project.PrimaryDirectory, "worktrees", taskId.ToString("N"));
        await store.BeginAsync(fixture.Project.Id, taskId, fixture.Project.PrimaryDirectory, null, target, fixture.Token);
        await store.MarkClaimedAsync(taskId, target,
            new DirectoryObjectIdentity(target, "volume", "object"), fixture.Token);
        await store.MarkGitCreatedAsync(taskId, target, fixture.Token);

        await fixture.Initializer.InitializeAsync(fixture.Token);

        var orphaned = Assert.Single(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(WorktreeCreationStatus.Orphaned, orphaned.Status);
        Assert.Equal(taskId, orphaned.TaskId);
        Assert.Equal(target, orphaned.TargetPath);
    }

    [Fact]
    public async Task StartupReconcilesIntentWhenTheMatchingTaskWasPersisted()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteWorktreeCreationIntentStore(fixture.Connections, TimeProvider.System);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Project.PrimaryDirectory, "worktrees", taskId.ToString("N"));
        await store.BeginAsync(fixture.Project.Id, taskId, fixture.Project.PrimaryDirectory, null, target, fixture.Token);
        await store.MarkClaimedAsync(taskId, target,
            new DirectoryObjectIdentity(target, "volume", "object"), fixture.Token);
        await store.MarkGitCreatedAsync(taskId, target, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        await fixture.Repository.SaveTaskAsync(new AgentTask(taskId, fixture.Project.Id, "persisted worktree",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Interrupted, now, now, target), fixture.Token);

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Empty(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            $"SELECT COUNT(*) FROM worktree_creation_intents WHERE task_id='{taskId}' AND status=4;"));
    }

    [Fact]
    public async Task FailedIntentCanBeAcknowledgedWithoutDeletingItsAuditRow()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteWorktreeCreationIntentStore(fixture.Connections, TimeProvider.System);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Project.PrimaryDirectory, "worktrees", taskId.ToString("N"));
        await store.BeginAsync(fixture.Project.Id, taskId, fixture.Project.PrimaryDirectory, null, target, fixture.Token);
        await store.MarkFailedAsync(taskId, target, "task_submission_failed", fixture.Token);

        Assert.Equal(WorktreeCreationStatus.Failed,
            Assert.Single(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token)).Status);
        await store.AcknowledgeAsync(taskId, target, fixture.Token);

        Assert.Empty(await store.ReadIncompleteAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            $"SELECT COUNT(*) FROM worktree_creation_intents WHERE task_id='{taskId}' AND status=6;"));
    }
}
