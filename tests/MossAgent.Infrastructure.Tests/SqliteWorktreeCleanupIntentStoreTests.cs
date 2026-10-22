using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteWorktreeCleanupIntentStoreTests
{
    [Fact]
    public async Task StagesAndCompletionAreConditionalAndCompletedRowsAreHidden()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests");
        var target = Path.Combine(root, "worktrees", taskId.ToString("N"));
        var quarantine = Path.Combine(root, "worktrees", $"{Guid.NewGuid():N}.quarantine");
        var store = new SqliteWorktreeCleanupIntentStore(
            fixture.Connections, TimeProvider.System);

        await store.BeginAsync(projectId, taskId, target, quarantine, fixture.Token);
        await store.MarkQuarantinedAsync(taskId, quarantine, fixture.Token);
        await store.MarkRegistrationDeletedAsync(taskId, quarantine, fixture.Token);
        var pending = Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token));
        Assert.Equal(WorktreeCleanupStage.RegistrationDeleted, pending.Stage);
        Assert.Equal(WorktreeCleanupState.Running, pending.State);

        await store.MarkCompletedAsync(taskId, quarantine, fixture.Token);
        Assert.Empty(await store.ReadIncompleteAsync(projectId, fixture.Token));
    }

    [Fact]
    public async Task FailedCleanupCanBeReusedOrAcknowledgedButRunningOwnerCannotAcknowledge()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests");
        var target = Path.Combine(root, "worktrees", taskId.ToString("N"));
        var quarantine = Path.Combine(root, "worktrees", $"{Guid.NewGuid():N}.quarantine");
        var store = new SqliteWorktreeCleanupIntentStore(
            fixture.Connections, TimeProvider.System);
        await store.BeginAsync(projectId, taskId, target, quarantine, fixture.Token);
        var running = Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AcknowledgeAsync(running, fixture.Token));

        await store.MarkFailedAsync(taskId, quarantine, "cleanup_failed", fixture.Token);
        Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token));
        var retryQuarantine = Path.Combine(root, "worktrees", $"{Guid.NewGuid():N}.quarantine");
        await store.BeginAsync(projectId, taskId, target, retryQuarantine, fixture.Token);
        Assert.Equal(WorktreeCleanupStage.Preparing,
            Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token)).Stage);
        await store.MarkFailedAsync(taskId, retryQuarantine, "cleanup_failed", fixture.Token);
        var retriedFailure = Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token));
        var otherInstance = new SqliteWorktreeCleanupIntentStore(
            fixture.Connections, TimeProvider.System);
        await otherInstance.AcknowledgeAsync(retriedFailure, fixture.Token);
        Assert.Empty(await store.ReadIncompleteAsync(projectId, fixture.Token));
    }

    [Fact]
    public async Task CompletedCleanupRemainsVisibleUntilTaskStopsReferencingTheTarget()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var projectId = fixture.Project.Id;
        var taskId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests");
        var target = Path.Combine(root, "worktrees", taskId.ToString("N"));
        var quarantine = Path.Combine(root, "worktrees", $"{Guid.NewGuid():N}.quarantine");
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, projectId, "cleanup completion", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, target);
        await fixture.Repository.SaveTaskAsync(task, fixture.Token);
        var store = new SqliteWorktreeCleanupIntentStore(
            fixture.Connections, TimeProvider.System);
        await store.BeginAsync(projectId, taskId, target, quarantine, fixture.Token);
        await store.MarkQuarantinedAsync(taskId, quarantine, fixture.Token);
        await store.MarkRegistrationDeletedAsync(taskId, quarantine, fixture.Token);
        await store.MarkCompletedAsync(taskId, quarantine, fixture.Token);

        var pending = Assert.Single(await store.ReadIncompleteAsync(projectId, fixture.Token));
        Assert.Equal(WorktreeCleanupState.Completed, pending.State);
        await store.AcknowledgeAsync(pending, fixture.Token);
        Assert.Empty(await store.ReadIncompleteAsync(projectId, fixture.Token));
    }
}
