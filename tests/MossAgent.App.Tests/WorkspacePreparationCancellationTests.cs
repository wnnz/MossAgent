using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePreparationCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelDuringFirstSave_PreservesInputAndSelectedAttachmentsAndNeverCallsProvider(bool withProject)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }
        };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "  取消后保留草稿。  ";
        var attachment = withProject ? AddAttachment(workspace) : null;
        workspace.PrimaryActionCommand.Execute(null);
        await entered.Task.WaitAsync(fixture.Token);
        Assert.True(workspace.IsRunning);
        Assert.False(workspace.HasActiveRun);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Equal("取消发送准备", workspace.PrimaryActionLabel);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal("  取消后保留草稿。  ", workspace.ComposerText);
        if (attachment is not null) Assert.Same(attachment, Assert.Single(workspace.Attachments.Items));
        Assert.Null(repository.SavedTask);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Empty(repository.Messages);
        Assert.Empty(fixture.Server.Requests);
        Assert.False(workspace.HasSendPreparationError);
        Assert.Equal("发送消息", workspace.PrimaryActionLabel);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingWorkspace_CancelsCooperativePreparationAndWaitsBeforeDisposal(bool dispose)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = async (_, token) => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); }
        };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "关闭时不能继续发送。";
        workspace.PrimaryActionCommand.Execute(null);
        await entered.Task.WaitAsync(fixture.Token);
        if (dispose) await workspace.DisposeAsync();
        else await workspace.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        Assert.False(workspace.IsRunning);
        Assert.True(workspace.IsShuttingDown);
        Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
        Assert.Null(repository.SavedTask);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task CancelAfterWorktreeCreation_CleansUnpersistedDirectoryWithoutLosingDraft()
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? path = null;
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = async (task, token) =>
            {
                path = task.WorktreePath;
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(repository, runs);
        await workspace.LoadAsync();
        workspace.ComposerText = "保留工作树创建前的输入。";
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await entered.Task.WaitAsync(fixture.Token);
        Assert.True(Directory.Exists(path));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.False(Directory.Exists(path));
        Assert.Equal("保留工作树创建前的输入。", workspace.ComposerText);
        Assert.False(workspace.HasSendPreparationError);
        Assert.False(workspace.Catalog.Environment.HasPreparationNotice);
        Assert.Empty(fixture.Server.Requests);
        Assert.Null(repository.SavedTask);
    }

    private static WorkspaceAttachmentViewModel AddAttachment(WorkspaceViewModel workspace)
    {
        var project = workspace.Catalog.SelectedProject!;
        var attachment = new WorkspaceAttachmentViewModel(new WorkspaceAttachmentContent(
            Path.Combine(project.PrimaryDirectory, "retained.txt"), "retained.txt", "synthetic content", 17), _ => { },
            authorizationStamp: WorkspaceAttachmentAuthorization.Create(project));
        workspace.Attachments.Items.Add(attachment);
        return attachment;
    }

    [Fact]
    public async Task CancellingOnePreparation_DoesNotCancelAnotherConversationInTheSameRuntime()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var original = fixture.Workspace.Catalog.SelectedTask!;
        var repository = new PausingWorkspaceRepository(fixture.Tasks);
        await using var workspace = fixture.CreateWorkspace(repository, fixture.Runs);
        await workspace.LoadAsync();
        workspace.Catalog.NewTaskCommand.Execute(null);
        workspace.ComposerText = "只取消这个新对话的准备。";
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await repository.Entered.Task.WaitAsync(fixture.Token);
            workspace.PrimaryActionCommand.Execute(null);
        }
        finally { repository.Release.TrySetResult(); }
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.True(fixture.Runs.IsRunning(original.Id));
        Assert.True(fixture.Workspace.HasActiveRun);
        Assert.Equal(original.Id, Assert.Single(await fixture.Tasks.GetTasksAsync(original.ProjectId, fixture.Token)).Id);
        Assert.Equal("只取消这个新对话的准备。", workspace.ComposerText);
        Assert.Single(fixture.Server.Requests);
    }
}
