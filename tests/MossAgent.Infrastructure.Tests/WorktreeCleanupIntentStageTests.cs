using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeCleanupIntentStageTests : IAsyncLifetime
{
    private GitWorktreeSecurityFixture? _fixture;
    private SqliteFailurePersistenceFixture? _storage;
    private GitWorktreeSecurityFixture Fixture => _fixture!;
    private SqliteFailurePersistenceFixture Storage => _storage!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await GitWorktreeSecurityFixture.CreateAsync();
        try
        {
            _storage = await SqliteFailurePersistenceFixture.CreateAsync();
            await _storage.Repository.SaveProjectAsync(_fixture.Project, _storage.Token);
        }
        catch { _fixture.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_storage is not null) await _storage.DisposeAsync();
        _fixture?.Dispose();
    }

    private GitWorktreeService Service() => new(Fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()),
        roots: new RootIdentityProbe(), connections: Storage.Connections);

    private SqliteWorktreeCleanupIntents Store() => new(Storage.Connections);

    [Fact]
    public async Task SuccessfulRemoveProgressesGitRemovedThenTaskDetachCompletes()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, Fixture.Project.Id, "cleanup task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, target);
        await Storage.Repository.SaveTaskAsync(task, Fixture.Token);

        await service.RemoveAsync(Fixture.Project, target, Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.GitRemoved);

        await Storage.Repository.SaveTaskAsync(task with
        {
            WorktreePath = null,
            UpdatedAt = DateTimeOffset.UtcNow
        }, Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Detached);
        Assert.Empty(await ((IWorktreeCleanupIntents)service).ReadIncompleteAsync(Fixture.Project, Fixture.Token));
    }

    [Fact]
    public async Task FailedRemoveIsMarkedWithErrorCode()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var now = DateTimeOffset.UtcNow;
        await Storage.Repository.SaveTaskAsync(new AgentTask(taskId, Fixture.Project.Id, "dirty task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Running, now, now, target), Fixture.Token);
        await File.WriteAllTextAsync(Path.Combine(target, "dirty.txt"), "uncommitted", Fixture.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveAsync(Fixture.Project, target, Fixture.Token));

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Failed, "git");
        var listed = Assert.Single(await ((IWorktreeCleanupIntents)service)
            .ReadIncompleteAsync(Fixture.Project, Fixture.Token));
        Assert.Equal(taskId, listed.TaskId);
    }

    [Fact]
    public async Task ReconcileOrphansStaleIntentAndAnnotatesTargetState()
    {
        var service = Service();
        var store = Store();
        var retainedTask = Guid.NewGuid();
        var removedTask = Guid.NewGuid();
        var retainedTarget = Fixture.Target(retainedTask);
        await store.RecordAsync(retainedTask, Fixture.Project, Fixture.Repository, retainedTarget, Fixture.Token);
        await store.RecordAsync(removedTask, Fixture.Project, Fixture.Repository,
            Fixture.Target(removedTask), Fixture.Token);
        Directory.CreateDirectory(retainedTarget);
        await Storage.ExecuteAsync("""
            UPDATE worktree_cleanup_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);

        await service.ReconcileAsync(Fixture.Token);

        await AssertCleanupStageAsync(retainedTask, WorktreeCleanupIntentStage.Orphaned, "retained");
        await AssertCleanupStageAsync(removedTask, WorktreeCleanupIntentStage.Orphaned, "removed");
        var orphans = await ((IWorktreeCleanupIntents)service).ReadIncompleteAsync(Fixture.Project, Fixture.Token);
        Assert.Equal(2, orphans.Count);
    }

    [Fact]
    public async Task ReconcileDetachesRecordedIntentWhoseTaskAlreadyLostWorktree()
    {
        var service = Service();
        var taskId = Storage.Task.Id;
        await Storage.ExecuteAsync($"""
            INSERT INTO worktree_cleanup_intents(task_id,project_id,repository,target,stage,created_at,updated_at)
            VALUES('{taskId}','{Fixture.Project.Id}','{Fixture.Repository}','{Fixture.Target(taskId)}',0,
                   '2000-01-01T00:00:00.0000000+00:00','2000-01-01T00:00:00.0000000+00:00');
            """);

        await service.ReconcileAsync(Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Detached);
    }

    [Fact]
    public async Task ReconcileCompletesGitRemovedIntentWhenTaskRowIsGone()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        await Storage.ExecuteAsync($"""
            INSERT INTO worktree_cleanup_intents(task_id,project_id,repository,target,stage,created_at,updated_at)
            VALUES('{taskId}','{Fixture.Project.Id}','{Fixture.Repository}','{Fixture.Target(taskId)}',1,
                   '2000-01-01T00:00:00.0000000+00:00','2000-01-01T00:00:00.0000000+00:00');
            """);

        await service.ReconcileAsync(Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Detached);
    }

    [Fact]
    public async Task ReconcileSkipsFreshInFlightIntent()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        await Store().RecordAsync(taskId, Fixture.Project, Fixture.Repository,
            Fixture.Target(taskId), Fixture.Token);

        await service.ReconcileAsync(Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Recorded);
        Assert.Empty(await ((IWorktreeCleanupIntents)service).ReadIncompleteAsync(Fixture.Project, Fixture.Token));
    }

    [Fact]
    public async Task RecordRefusesConcurrentIntentButAllowsRetryAfterFailure()
    {
        var store = Store();
        var taskId = Guid.NewGuid();
        var target = Fixture.Target(taskId);
        await store.RecordAsync(taskId, Fixture.Project, Fixture.Repository, target, Fixture.Token);

        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => store.RecordAsync(taskId, Fixture.Project, Fixture.Repository, target, Fixture.Token));

        await store.MarkFailedAsync(taskId, "git", Fixture.Token);
        await store.RecordAsync(taskId, Fixture.Project, Fixture.Repository, target, Fixture.Token);
        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Recorded);
    }

    [Fact]
    public async Task AcknowledgeClosesOrphanedIntent()
    {
        var service = Service();
        var store = Store();
        var taskId = Guid.NewGuid();
        await store.RecordAsync(taskId, Fixture.Project, Fixture.Repository,
            Fixture.Target(taskId), Fixture.Token);
        await Storage.ExecuteAsync("""
            UPDATE worktree_cleanup_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);
        await service.ReconcileAsync(Fixture.Token);

        await ((IWorktreeCleanupIntents)service).AcknowledgeAsync(taskId, Fixture.Token);

        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Acknowledged, "removed");
        Assert.Empty(await ((IWorktreeCleanupIntents)service).ReadIncompleteAsync(Fixture.Project, Fixture.Token));
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => ((IWorktreeCleanupIntents)service).AcknowledgeAsync(taskId, Fixture.Token));
    }

    private async Task AssertCleanupStageAsync(Guid taskId, WorktreeCleanupIntentStage stage, string? errorCode = null)
    {
        await using var connection = Storage.Connections.Create();
        await connection.OpenAsync(Fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT stage,error_code FROM worktree_cleanup_intents WHERE task_id=$task";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Fixture.Token);
        Assert.True(await reader.ReadAsync(Fixture.Token));
        Assert.Equal((int)stage, reader.GetInt32(0));
        Assert.Equal(errorCode, reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
