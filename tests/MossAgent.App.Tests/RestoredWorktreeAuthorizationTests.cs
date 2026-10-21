using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RestoredWorktreeAuthorizationTests
{
    [Theory]
    [InlineData("source")]
    [InlineData("another-task")]
    [InlineData("relative")]
    [InlineData("empty")]
    [InlineData("revoked")]
    public async Task ForgedPersistedWorktreeBlocksToolsBranchReadAndResumeWithoutConsumingDraft(string location)
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var task = await CreateTaskAsync(owner, ApprovalPolicy.FullAccess);
        var project = owner.Project;
        if (location == "revoked")
        {
            project = project with { AuthorizedDirectories = [] };
            await fixture.Tasks.SaveProjectAsync(project, fixture.Token);
        }
        var worktrees = new GitWorktreeService(new AppDataPaths(fixture.DataRoot), fixture.Git);
        var invalid = location switch
        {
            "source" => owner.Project.PrimaryDirectory,
            "another-task" => await worktrees.CreateAsync(owner.Project, Guid.NewGuid(), fixture.Token),
            "relative" => "worktrees/relative",
            "revoked" => task.WorktreePath!,
            _ => string.Empty
        };
        var forged = task with { WorktreePath = invalid };
        await fixture.Tasks.SaveTaskAsync(forged, fixture.Token);
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var workspace = reopened.GetRequiredService<MainWindowViewModel>().Workspace;
        await workspace.LoadAsync();
        workspace.Catalog.SelectedTask = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => workspace.Messages.Count == 1 && workspace.Drafts.Scope?.TaskId == task.Id);
        var context = reopened.GetRequiredService<WorkspaceTaskPanelCoordinator>().Attach(forged, project);
        Assert.NotNull(context.WorkspaceGuard);
        var result = await reopened.GetRequiredService<IToolExecutor>().ExecuteAsync(new("forged-root", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" })), context, fixture.Token);
        Assert.Equal(location == "revoked" ? "workspace_authorization_changed" : "workspace_denied", result.ErrorCode);
        Assert.DoesNotContain("committed baseline", result.Content ?? string.Empty);
        var previews = reopened.GetRequiredService<FilePreviewPaneViewModel>();
        await previews.OpenAsync("README.md", fixture.Token);
        Assert.DoesNotContain(previews.Tabs, tab => tab.IsReady || tab.Text.Contains("committed baseline"));
        var diff = reopened.GetRequiredService<DiffPaneViewModel>();
        await diff.ReviewAsync(task.Id, fixture.Token);
        Assert.Empty(diff.Files);
        Assert.Equal(InspectorText.Get("DiffFailed"), diff.Status);
        var terminal = reopened.GetRequiredService<TerminalPaneViewModel>();
        Assert.True(terminal.StartCommand.CanExecute(null));
        await terminal.StartCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get(location == "revoked" ? "TerminalAuthorizationChanged" : "TerminalPathDenied"), terminal.Status);
        var terminalStates = Assert.IsAssignableFrom<ITerminalStateSource>(reopened.GetRequiredService<ITerminalSessionManager>());
        Assert.Null(terminalStates.GetSnapshot(task.Id));
        using var branchProbe = new RepositoryStatusProbe();
        await using var status = new WorkspaceRepositoryStatusViewModel(branchProbe, worktrees);
        await status.RefreshAsync(project, forged);
        Assert.Empty(branchProbe.Reads);
        workspace.ComposerText = "retained denied follow-up";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.HasSendPreparationError);
        Assert.Equal("retained denied follow-up", workspace.ComposerText);
        Assert.Equal(WorkspaceText.Get("TaskEnvironmentAuthorizationFailure"), workspace.SendPreparationError);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Equal(AgentTaskStatus.Completed, Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token)).Status);
        Assert.Empty(fixture.Server.Requests);
        Assert.True(Directory.Exists(task.WorktreePath));
    }

    [Fact]
    public async Task ValidRestoredWorktreeReadsAndContinuesWithoutAutomaticReplay()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var task = await CreateTaskAsync(owner);
        await using var reopened = AppComposition.CreateServices(fixture.DataRoot);
        var workspace = reopened.GetRequiredService<MainWindowViewModel>().Workspace;
        await workspace.LoadAsync();
        workspace.Catalog.SelectedTask = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => workspace.Messages.Count == 1 && workspace.Drafts.Scope?.TaskId == task.Id);
        Assert.Empty(fixture.Server.Requests);
        var context = reopened.GetRequiredService<WorkspaceTaskPanelCoordinator>().CreateContext(task, owner.Project);
        var result = await reopened.GetRequiredService<IToolExecutor>().ExecuteAsync(new("restored-root", "filesystem.read_file",
            JsonSerializer.SerializeToElement(new { path = "README.md" })), context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("committed baseline", result.Content);
        workspace.ComposerText = "continue validated worktree";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(task.WorktreePath, workspace.Catalog.SelectedTask?.WorktreePath);
        Assert.Equal(2, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count(message => message.Role == MessageRole.User));
    }

    private static async Task<AgentTask> CreateTaskAsync(WorkspaceWorktreeGitFixture owner, ApprovalPolicy approvalPolicy = ApprovalPolicy.ReadOnly)
    {
        var fixture = owner.Fixture;
        var taskId = Guid.NewGuid();
        var path = await fixture.Worktrees.CreateAsync(owner.Project, taskId, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, owner.Project.Id, "restored fixture", approvalPolicy,
            AgentTaskStatus.Completed, now, now, path);
        await fixture.Tasks.SaveTaskWithMessagesAsync(task,
            [new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "saved prompt", now)], fixture.Token);
        return task;
    }
}
