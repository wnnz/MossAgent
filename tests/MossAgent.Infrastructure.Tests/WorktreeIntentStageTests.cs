using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeIntentStageTests : IAsyncLifetime
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

    [Fact]
    public async Task SuccessfulCreateProgressesThroughClaimedAndGitCreatedThenSaveConsumes()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);

        await AssertIntentStageAsync(taskId, WorktreeCreationIntentStage.GitCreated);

        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, Fixture.Project.Id, "staged task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, target);
        await Storage.Repository.SaveTaskAsync(task, Fixture.Token);

        await AssertIntentStageAsync(taskId, WorktreeCreationIntentStage.Consumed);
        Assert.Empty(await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token));
    }

    [Fact]
    public async Task FailedCreateIsMarkedWithErrorCode()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var claims = new FailingClaimFactory();
        var failing = new GitWorktreeService(Fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()),
            roots: new RootIdentityProbe(), connections: Storage.Connections, targets: claims);

        await Assert.ThrowsAnyAsync<Exception>(() => failing.CreateAsync(Fixture.Project, taskId, Fixture.Token));
        await AssertIntentStageAsync(taskId, WorktreeCreationIntentStage.Failed, "authorization");

        var listed = Assert.Single(await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token));
        Assert.Equal(taskId, listed.TaskId);
        Assert.Equal("authorization", listed.ErrorCode);
    }

    [Fact]
    public async Task ReconcileOrphansUnconsumedIntentAndKeepsConsumed()
    {
        var service = Service();
        var consumedTask = Guid.NewGuid();
        var orphanedTask = Guid.NewGuid();
        var consumedTarget = await service.CreateAsync(Fixture.Project, consumedTask, Fixture.Token);
        var now = DateTimeOffset.UtcNow;
        await Storage.Repository.SaveTaskAsync(
            new AgentTask(consumedTask, Fixture.Project.Id, "consumed", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Running, now, now, consumedTarget), Fixture.Token);
        var orphanedTarget = Path.Combine(Fixture.Paths.WorktreesPath, orphanedTask.ToString("N"));
        await new SqliteWorktreeCreationIntents(Storage.Connections).RecordAsync(
            orphanedTask, Fixture.Project, Fixture.Project.PrimaryDirectory, null, "seed", orphanedTarget, Fixture.Token);
        await Storage.ExecuteAsync("""
            UPDATE worktree_creation_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);

        await service.ReconcileAsync(Fixture.Token);

        await AssertIntentStageAsync(consumedTask, WorktreeCreationIntentStage.Consumed);
        await AssertIntentStageAsync(orphanedTask, WorktreeCreationIntentStage.Orphaned, "missing");
        var incomplete = Assert.Single(await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token));
        Assert.Equal(orphanedTask, incomplete.TaskId);
        Assert.Equal(WorktreeCreationIntentStage.Orphaned, incomplete.Stage);
    }

    [Fact]
    public async Task AcknowledgeClosesOrphanedIntent()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N"));
        await new SqliteWorktreeCreationIntents(Storage.Connections).RecordAsync(
            taskId, Fixture.Project, Fixture.Project.PrimaryDirectory, null, "seed", target, Fixture.Token);
        await Storage.ExecuteAsync("""
            UPDATE worktree_creation_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);
        await service.ReconcileAsync(Fixture.Token);

        await service.AcknowledgeAsync(taskId, Fixture.Token);
        await AssertIntentStageAsync(taskId, WorktreeCreationIntentStage.Acknowledged, "missing");
        Assert.Empty(await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token));
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => service.AcknowledgeAsync(taskId, Fixture.Token));
    }

    private async Task AssertIntentStageAsync(Guid taskId, WorktreeCreationIntentStage stage, string? errorCode = null)
    {
        await using var connection = Storage.Connections.Create();
        await connection.OpenAsync(Fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT stage,error_code FROM worktree_creation_intents WHERE task_id=$task";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Fixture.Token);
        Assert.True(await reader.ReadAsync(Fixture.Token));
        Assert.Equal((int)stage, reader.GetInt32(0));
        Assert.Equal(errorCode, reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    [Fact]
    public async Task ReconcileSkipsFreshInFlightIntent()
    {
        var service = Service();
        var taskId = Guid.NewGuid();
        var target = Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N"));
        await new SqliteWorktreeCreationIntents(Storage.Connections).RecordAsync(
            taskId, Fixture.Project, Fixture.Project.PrimaryDirectory, null, "seed", target, Fixture.Token);

        await service.ReconcileAsync(Fixture.Token);

        await AssertIntentStageAsync(taskId, WorktreeCreationIntentStage.Recorded);
        Assert.Empty(await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token));
    }

    [Fact]
    public async Task ReconcileAnnotatesOrphanTargetState()
    {
        var service = Service();
        var missingTask = Guid.NewGuid();
        var retainedTask = Guid.NewGuid();
        var intents = new SqliteWorktreeCreationIntents(Storage.Connections);
        var missingTarget = Path.Combine(Fixture.Paths.WorktreesPath, missingTask.ToString("N"));
        var retainedTarget = Path.Combine(Fixture.Paths.WorktreesPath, retainedTask.ToString("N"));
        await intents.RecordAsync(missingTask, Fixture.Project, Fixture.Project.PrimaryDirectory,
            null, "seed", missingTarget, Fixture.Token);
        await intents.RecordAsync(retainedTask, Fixture.Project, Fixture.Project.PrimaryDirectory,
            null, "seed", retainedTarget, Fixture.Token);
        Directory.CreateDirectory(retainedTarget);
        await File.WriteAllTextAsync(Path.Combine(retainedTarget, ".git"),
            $"gitdir: {Fixture.Project.PrimaryDirectory}/.git/worktrees/{retainedTask:N}\n", Fixture.Token);
        await Storage.ExecuteAsync("""
            UPDATE worktree_creation_intents SET updated_at='2000-01-01T00:00:00.0000000+00:00'
            WHERE stage<3;
            """);

        await service.ReconcileAsync(Fixture.Token);

        var orphans = await service.ReadIncompleteAsync(Fixture.Project, Fixture.Token);
        Assert.Equal(2, orphans.Count);
        Assert.Equal("missing", Assert.Single(orphans, i => i.TaskId == missingTask).ErrorCode);
        Assert.Equal("retained", Assert.Single(orphans, i => i.TaskId == retainedTask).ErrorCode);
    }

    private sealed class FailingClaimFactory : IWorktreeTargetClaimFactory
    {
        public Task<IWorktreeTargetClaim> CreateAsync(string target, CancellationToken token) =>
            throw new WorktreeAuthorizationException();
    }
}
