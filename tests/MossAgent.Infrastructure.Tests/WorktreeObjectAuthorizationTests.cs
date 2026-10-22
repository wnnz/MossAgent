using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;
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
        var task = await fixture.SaveTaskAsync(id, path);
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
        var task = await fixture.SaveTaskAsync(id, path);
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

    [Fact]
    public async Task ExplicitReauthorizationAdvancesRevisionAndInvalidatesOldProjectLease()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path);
        var previous = Assert.IsType<WorktreeRootAuthorization>(
            await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        var replacement = fixture.Roots.Replace(path);

        var result = await fixture.Service.ReauthorizeAsync(
            fixture.Project, task, fixture.Token);

        Assert.Equal(previous.Identity, result.PreviousIdentity);
        Assert.Equal(replacement, result.Identity);
        Assert.NotEqual(previous.GrantId, result.GrantId);
        Assert.Equal(fixture.Project.AuthorizationRevision + 1,
            result.AuthorizationRevision);
        var current = Assert.IsType<WorktreeRootAuthorization>(
            await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.Equal(result.GrantId, current.GrantId);
        Assert.Equal(replacement, current.Identity);
        var projects = await fixture.Storage.Repository.GetProjectsAsync(fixture.Token);
        Assert.Equal(result.AuthorizationRevision,
            Assert.Single(projects, item => item.Id == fixture.Project.Id)
                .AuthorizationRevision);
        var changes = new ProjectAuthorizationChanges();
        await using var monitor = new ProjectAuthorizationMonitor(
            fixture.Storage.Repository, changes);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(
            () => monitor.ValidateAsync(fixture.Project, fixture.Token));
        await fixture.Reopen().ValidateAsync(fixture.Project with
        {
            AuthorizationRevision = result.AuthorizationRevision
        }, id, path, fixture.Token);
        Assert.Equal(1, await fixture.Storage.CountAsync(
            "SELECT COUNT(*) FROM worktree_root_authorization_history WHERE task_id='"
            + id + "';"));
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }

    [Fact]
    public async Task RunningTaskCannotReplaceItsWorktreeAuthorization()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path) with
        {
            Status = AgentTaskStatus.Running,
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        await fixture.Storage.Repository.SaveTaskAsync(task, fixture.Token);
        var replacement = fixture.Roots.Replace(path);

        await Assert.ThrowsAsync<WorktreeAuthorizationException>(
            () => fixture.Service.ReauthorizeAsync(fixture.Project, task, fixture.Token));

        var grant = Assert.IsType<WorktreeRootAuthorization>(
            await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.NotEqual(replacement, grant.Identity);
        Assert.Equal(0, fixture.Roots.ActiveSessions);
    }
}
