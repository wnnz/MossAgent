using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceGitPreparationCancellationTests
{
    [Fact]
    public async Task CancellingRealCheckoutHook_StopsOwnedGitAndSurfacesTheRetainedDirectoryWithoutForceRemoval()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var marker = await InstallHookAsync(owner);
        using var runs = new WorkspaceRunTracker();
        var repository = new TestWorkspaceRepository();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "停止正在创建工作树的 Git。";
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => File.Exists(marker));
        var path = Assert.Single(Directory.GetDirectories(Path.Combine(fixture.DataRoot, "worktrees")));
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(workspace.HasSendPreparationError);
        Assert.True(workspace.Catalog.Environment.HasPreparationNotice);
        Assert.Contains(path, workspace.SendPreparationError);
        Assert.True(Directory.Exists(path));
        Assert.Equal("停止正在创建工作树的 Git。", workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        Assert.Null(repository.SavedTask);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        var listed = (await owner.GitAsync(["worktree", "list", "--porcelain"])).StandardOutput.Replace('\\', '/');
        Assert.Contains(path.Replace('\\', '/'), listed);
        await owner.GitAsync(["worktree", "remove", path]);
    }

    [Fact]
    public async Task PartialCreationFailureFromPort_RetainsRecoveryPathAndDoesNotBlindlyRemoveAnUnknownOwner()
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe
        {
            CreateHandler = _ => throw new GitWorktreeCreationException("managed-retained-path", new IOException("synthetic-secret-git-error"))
        };
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", "fixture-root", ["fixture-root"], DateTimeOffset.UtcNow);
        var factory = new WorkspaceTaskFactory(repository, probe);
        var failure = await Assert.ThrowsAsync<WorkspaceWorktreePreparationException>(() => factory.PrepareAsync(project, null,
            "prompt", ApprovalPolicy.ReadOnly, [], TestContext.Current.CancellationToken, useWorktree: true));
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.ReportPreparationFailure(failure);
        Assert.Contains("managed-retained-path", environment.PreparationNotice);
        Assert.DoesNotContain("synthetic-secret", environment.PreparationNotice);
        Assert.Empty(probe.Removed);
        Assert.Null(repository.SavedTask);
    }

    private static async Task<string> InstallHookAsync(WorkspaceWorktreeGitFixture owner)
    {
        var fixture = owner.Fixture;
        var hooks = Path.Combine(fixture.DataRoot, "preparation-hooks");
        Directory.CreateDirectory(hooks);
        var marker = Path.Combine(hooks, "entered");
        var literal = marker.Replace('\\', '/').Replace("'", "'\"'\"'");
        await File.WriteAllTextAsync(Path.Combine(hooks, "post-checkout"),
            $"#!/bin/sh\nprintf ready > '{literal}'\nwhile :; do sleep 1; done\n", new UTF8Encoding(false), fixture.Token);
        await owner.GitAsync(["config", "core.hooksPath", hooks]);
        return marker;
    }
}
