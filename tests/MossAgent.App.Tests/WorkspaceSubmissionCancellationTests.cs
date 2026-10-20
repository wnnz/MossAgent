using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSubmissionCancellationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancellationRespectsTheCommitBoundaryAndNeverStartsTheProvider(bool afterCommit, bool withProject)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.Drafts.FlushAsync();
        var repository = new SubmissionWorkspaceRepository(fixture.Tasks) { PauseAfterCommit = afterCommit };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs, conversationDrafts: fixture.Drafts);
        await workspace.LoadAsync();
        workspace.ComposerText = "cancelled input";
        await workspace.Drafts.FlushAsync();
        var scope = workspace.Drafts.Scope;
        workspace.PrimaryActionCommand.Execute(null);
        await repository.Entered.Task.WaitAsync(fixture.Token);
        Assert.True(workspace.IsRunning);
        Assert.False(workspace.HasActiveRun);
        workspace.PrimaryActionCommand.Execute(null);
        if (afterCommit)
        {
            Assert.True(workspace.IsRunning);
            Assert.Empty((await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
            repository.Release.TrySetResult();
        }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Empty(fixture.Server.Requests);
        Assert.False(workspace.HasSendPreparationError);
        if (!afterCommit)
        {
            Assert.Equal("cancelled input", workspace.ComposerText);
            Assert.Equal("cancelled input", (await fixture.Drafts.GetAsync(scope, fixture.Token))!.Text);
            Assert.Empty(await fixture.Tasks.GetTasksAsync(scope.ProjectId, fixture.Token));
            Assert.Null(workspace.Catalog.SelectedTask);
            return;
        }
        var task = Assert.Single(await fixture.Tasks.GetTasksAsync(scope.ProjectId, fixture.Token));
        Assert.Equal(AgentTaskStatus.Cancelled, task.Status);
        Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(task.Id, workspace.Drafts.Scope.TaskId);
        Assert.Empty(workspace.ComposerText);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Contains("消息已保存", workspace.Activity);
    }
}
