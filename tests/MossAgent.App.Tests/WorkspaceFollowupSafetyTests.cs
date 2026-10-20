using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedModelAvailabilityOrProjectScopePreventsAutomaticProviderRequests(bool changeScope)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: changeScope);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "配置变化前保存的排队消息";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "保留前台草稿";
        if (changeScope)
        {
            var project = workspace.Catalog.SelectedProject!;
            await fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = [project.PrimaryDirectory, fixture.DataRoot] }, fixture.Token);
        }
        else await fixture.SetSelectedModelEnabledAsync(false);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.HasNotice);
        Assert.Single(fixture.Server.Requests);
        Assert.Single(workspace.Queue.Items);
        Assert.Equal("保留前台草稿", workspace.ComposerText);
        Assert.Contains(changeScope ? "授权目录已变化" : "模型或提供商不可用", workspace.Queue.Notice);
        var queue = await fixture.Followups.GetAsync(workspace.Catalog.SelectedTask!.Id, fixture.Token);
        Assert.Single(queue.Items);
        Assert.DoesNotContain(await fixture.Tasks.GetMessagesAsync(queue.TaskId, fixture.Token), message => message.Id == queue.Items[0].Id);
    }

    [Fact]
    public async Task ConcurrentQueueEditPreservesTheEditorAndDoesNotOverwriteTheExternalChange()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "编辑前内容";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        var row = Assert.Single(workspace.Queue.Items);
        row.EditCommand.Execute(null);
        workspace.Queue.Editor!.EditText = "本窗口保留的编辑草稿";
        var current = await fixture.Followups.GetAsync(workspace.Catalog.SelectedTask!.Id, fixture.Token);
        Assert.True(await fixture.Followups.SaveAsync(current with { Items = [current.Items[0] with { Prompt = "外部修改" }] }, [], fixture.Token));
        await workspace.Queue.SaveEditCommand.ExecuteAsync(null);
        Assert.Equal("本窗口保留的编辑草稿", workspace.Queue.Editor.EditText);
        Assert.Contains("发生变化", workspace.Queue.Notice);
        Assert.Equal("外部修改", Assert.Single((await fixture.Followups.GetAsync(current.TaskId, fixture.Token)).Items).Prompt);
        await workspace.Queue.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("外部修改", Assert.Single(workspace.Queue.Items).Prompt);
        Assert.Equal("本窗口保留的编辑草稿", workspace.Queue.Editor.EditText);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
    }

    [Fact]
    public async Task ReconstructedWorkspaceLoadsPendingEntriesWithoutAutomaticallyStartingThem()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "恢复后手动继续";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "重建后保留的草稿";
        await workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var restored = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts);
        await restored.LoadAsync();
        await fixture.WaitUntilAsync(() => restored.Queue.Items.Count == 1);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal("重建后保留的草稿", restored.ComposerText);
        Assert.True(restored.Queue.ContinueCommand.CanExecute(null));
        Assert.Contains("暂停", restored.Queue.Notice);
        fixture.Server.ReleaseResponses();
        await restored.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !restored.IsRunning);
        Assert.Empty(restored.Queue.Items);
        Assert.Equal("重建后保留的草稿", restored.ComposerText);
        await runs.StopAsync();
    }

    [Fact]
    public async Task IncompatibleDraftStoreDoesNotExposeQueueSubmission()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs);
        await workspace.LoadAsync();
        Assert.False(workspace.Queue.IsAvailable);
        Assert.False(workspace.Queue.EnqueueCommand.CanExecute(null));
        Assert.False(workspace.Queue.ContinueCommand.CanExecute(null));
        Assert.Empty(fixture.Server.Requests);
    }
}
