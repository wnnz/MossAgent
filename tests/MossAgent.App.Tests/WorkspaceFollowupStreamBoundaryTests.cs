using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupStreamBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingProviderCompletionPausesTheQueueAndManualContinuationSendsOnlyTheNextMessage(bool empty)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        fixture.Server.InterruptNextResponse(empty);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        workspace.ComposerText = "断流后等待手动续发";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "断流时正在编辑的下一份草稿";
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && (workspace.Catalog.SelectedTask?.Status != AgentTaskStatus.Failed
            || workspace.Queue.HasNotice && workspace.Queue.Items.Count == 1));
        Assert.Single(fixture.Server.Requests);
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
        Assert.Single(workspace.Queue.Items);
        Assert.Contains("暂停", workspace.Queue.Notice);
        Assert.Equal("断流时正在编辑的下一份草稿", workspace.ComposerText);
        var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        Assert.Single(history, message => message.Role == MessageRole.User);
        Assert.Equal(!empty, history.Any(message => message.Role == MessageRole.Assistant
            && message.Content.Contains("模型选择链路已完成。", StringComparison.Ordinal)));
        Assert.DoesNotContain(history, message => message.Content == "断流后等待手动续发");
        var stored = Assert.Single(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
        Assert.Equal(AgentTaskStatus.Failed, stored.Status);

        Assert.True(workspace.Queue.ContinueCommand.CanExecute(null));
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Empty(workspace.Queue.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
        Assert.Equal("断流时正在编辑的下一份草稿", workspace.ComposerText);
        history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        Assert.Equal(2, history.Count(message => message.Role == MessageRole.User));
        Assert.Single(history, message => message.Content == "断流后等待手动续发");
        Assert.Equal(taskId, workspace.Catalog.SelectedTask?.Id);
    }
}
