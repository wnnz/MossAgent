using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeRemovalGuardTests : IAsyncLifetime
{
    private GitWorktreeSecurityFixture? _fixture;
    private SqliteFailurePersistenceFixture? _storage;
    private readonly RootIdentityProbe _roots = new();
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

    private GitWorktreeService Service(IWorktreeRemovalGuardFactory? removals) => new(Fixture.Paths,
        new GitProcessRunner(new FixtureTaskProcessLauncher()),
        roots: _roots, connections: Storage.Connections, removals: removals);

    [Fact]
    public async Task SuccessfulRemoveOpensGuardWithEnrolledIdentityAndConfirmsOnce()
    {
        var guards = new StubWorktreeRemovalGuardFactory();
        var service = Service(guards);
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);
        var grant = await service.ReadAuthorizationAsync(taskId, Fixture.Token);

        await service.RemoveAsync(Fixture.Project, target, Fixture.Token);

        var guard = Assert.Single(guards.Guards);
        Assert.Equal(target, guard.Path);
        Assert.Equal(Assert.IsType<WorktreeRootAuthorization>(grant).Identity, guard.Expected);
        Assert.Equal(1, guard.Confirmations);
        Assert.True(guard.Disposed);
        Assert.False(Directory.Exists(target));
        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.GitRemoved);
    }

    [Fact]
    public async Task GuardOpenAuthorizationFailureFailsBeforeGitRemoveRuns()
    {
        var guards = new StubWorktreeRemovalGuardFactory(openFailure: new ProjectRootAuthorizationException());
        var service = Service(guards);
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);

        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(
            () => service.RemoveAsync(Fixture.Project, target, Fixture.Token));

        Assert.Empty(guards.Guards);
        Assert.True(Directory.Exists(target));
        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Failed, "authorization");
    }

    [Fact]
    public async Task GuardConfirmFailureFailsClosedAfterGitRemovedTheTarget()
    {
        var guards = new StubWorktreeRemovalGuardFactory(confirmFailure: new ProjectRootAuthorizationException());
        var service = Service(guards);
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);

        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(
            () => service.RemoveAsync(Fixture.Project, target, Fixture.Token));

        var guard = Assert.Single(guards.Guards);
        Assert.Equal(1, guard.Confirmations);
        Assert.True(guard.Disposed);
        Assert.False(Directory.Exists(target));
        await AssertCleanupStageAsync(taskId, WorktreeCleanupIntentStage.Failed, "authorization");
    }

    [Fact]
    public async Task GuardIsSkippedWhenNoAuthorizationEnrollmentExists()
    {
        var guards = new StubWorktreeRemovalGuardFactory();
        var service = new GitWorktreeService(Fixture.Paths,
            new GitProcessRunner(new FixtureTaskProcessLauncher()), removals: guards);
        var taskId = Guid.NewGuid();
        var target = await service.CreateAsync(Fixture.Project, taskId, Fixture.Token);

        await service.RemoveAsync(Fixture.Project, target, Fixture.Token);

        Assert.Empty(guards.Guards);
        Assert.False(Directory.Exists(target));
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
