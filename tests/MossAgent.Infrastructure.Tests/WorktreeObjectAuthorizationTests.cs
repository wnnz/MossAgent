using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeObjectAuthorizationTests
{
    [Fact]
    public async Task CreatedGrantSurvivesRestartAndRejectsReplacementForExecutionConfirmationAndCleanup()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var grant = Assert.IsType<WorktreeRootAuthorization>(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.Equal(fixture.Project.Id, grant.ProjectId);
        Assert.NotEqual(Guid.Empty, grant.GrantId);
        await fixture.Reopen().ValidateAsync(fixture.Project, id, path, fixture.Token);
        fixture.Roots.Replace(path);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Service.ValidateAsync(fixture.Project, id, path, fixture.Token));
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Service.RemoveAsync(fixture.Project, path, fixture.Token));
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Service.ConfirmLegacyAsync(fixture.Project, task, fixture.Token));
        Assert.Equal(grant, await fixture.Reopen().ReadAuthorizationAsync(id, fixture.Token));
        Assert.True(File.Exists(Path.Combine(path, ".git")));
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }

    [Fact]
    public async Task LegacyWorktreeRequiresExplicitConfirmationWithoutChangingTaskOrReplayingHistory()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Git.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path, seedIntent: true);
        Assert.Null(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Service.ValidateAsync(fixture.Project, id, path, fixture.Token));
        await fixture.Service.ConfirmLegacyAsync(fixture.Project, task, fixture.Token);
        await fixture.Reopen().ValidateAsync(fixture.Project, id, path, fixture.Token);
        Assert.Equal(task, Assert.Single(await fixture.Storage.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Empty(await fixture.Storage.Repository.GetMessagesAsync(id, fixture.Token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.ValidateAsync(fixture.Project, Guid.NewGuid(), path, fixture.Token));
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }

    [Theory]
    [InlineData("updated")]
    [InlineData("running")]
    [InlineData("project")]
    public async Task ConditionalConfirmationRejectsStaleOrRunningScope(string changed)
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Git.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path, seedIntent: true);
        if (changed == "project")
            await fixture.Storage.Repository.SaveProjectAsync(fixture.Project with { AuthorizedDirectories = [] }, fixture.Token);
        else
            await fixture.Storage.Repository.SaveTaskAsync(task with
            {
                Status = changed == "running" ? AgentTaskStatus.Running : task.Status,
                UpdatedAt = task.UpdatedAt.AddSeconds(1)
            }, fixture.Token);
        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => fixture.Service.ConfirmLegacyAsync(fixture.Project, task, fixture.Token));
        Assert.Null(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.Equal(0, fixture.Roots.ActiveSessions);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task PersistenceFailureRetainsCreatedDirectoryWithoutGrantAndReleasesSession()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        await fixture.Storage.ExecuteAsync("CREATE TRIGGER refuse_worktree BEFORE INSERT ON worktree_root_authorizations BEGIN SELECT RAISE(ABORT,'synthetic refusal'); END;");
        var id = Guid.NewGuid();
        var failure = await Assert.ThrowsAsync<GitWorktreeCreationException>(() => fixture.Service.CreateAsync(fixture.Project, id, fixture.Token));
        Assert.True(Directory.Exists(failure.WorktreePath));
        Assert.Null(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }
}
