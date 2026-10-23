using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RestoredWorktreeCleanupAuthorizationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignTaskPathOrProjectCannotRemoveAnOwnedWorktreeOrClearStoredMetadata(bool wrongProject)
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var worktrees = new GitWorktreeService(new AppDataPaths(fixture.DataRoot), fixture.Git);
        var ownerId = Guid.NewGuid();
        var path = await worktrees.CreateAsync(owner.Project, ownerId, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(wrongProject ? ownerId : Guid.NewGuid(), owner.Project.Id, "cleanup fixture",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, path);
        var intents = new SqliteWorktreeCreationIntents(new SqliteConnectionFactory(new AppDataPaths(fixture.DataRoot)));
        await intents.RecordAsync(task.Id, owner.Project, owner.Project.PrimaryDirectory, null, "seed", path, fixture.Token);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        var project = wrongProject ? owner.Project with { Id = Guid.NewGuid() } : owner.Project;
        var before = (await owner.GitAsync(["worktree", "list", "--porcelain"])).StandardOutput;
        var cleanup = new WorkspaceWorktreeCleanupService(fixture.Tasks, worktrees);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => cleanup.CleanupAsync(project, task, fixture.Token));

        Assert.True(Directory.Exists(path));
        Assert.True(File.Exists(Path.Combine(path, ".git")));
        Assert.Equal(path, Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).WorktreePath);
        Assert.Equal(before, (await owner.GitAsync(["worktree", "list", "--porcelain"])).StandardOutput);
        Assert.Empty(fixture.Server.Requests);
    }
}
