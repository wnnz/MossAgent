using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupLifecycleTests
{
    [Fact]
    public async Task ProviderFailurePausesPendingInputWithoutSendingIt()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        fixture.Server.FailNextResponse();
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "提供商失败后不得自动续发";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Failed, workspace.Catalog.SelectedTask?.Status);
        Assert.Single(fixture.Server.Requests);
        Assert.Single(workspace.Queue.Items);
        Assert.Contains("暂停", workspace.Queue.Notice);
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Empty(workspace.Queue.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
    }

    [Fact]
    public async Task TasksOwnIndependentQueuesAndForegroundDraftsDuringConcurrentRuns()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var first = workspace.Catalog.SelectedTask!;
        workspace.ComposerText = "first-task-queued";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "first-task-next-draft";
        workspace.Catalog.NewChatCommand.Execute(null);
        await workspace.Drafts.LoadCurrentAsync();
        workspace.ComposerText = "second-task-initial";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.HasActiveRun && workspace.Catalog.SelectedTask?.Id != first.Id);
        var second = workspace.Catalog.SelectedTask!;
        workspace.ComposerText = "second-task-queued";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.ComposerText = "second-task-next-draft";
        workspace.Catalog.SelectTaskCommand.Execute(first);
        await workspace.Drafts.LoadCurrentAsync();
        await workspace.Queue.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("first-task-next-draft", workspace.ComposerText);
        Assert.Equal("first-task-queued", Assert.Single(workspace.Queue.Items).Prompt);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 4 && !fixture.Runs.IsRunning(first.Id) && !fixture.Runs.IsRunning(second.Id));
        Assert.Equal(first.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal("first-task-next-draft", workspace.ComposerText);
        Assert.DoesNotContain(workspace.Messages, message => message.Content.Contains("second-task", StringComparison.Ordinal));
        workspace.Catalog.SelectTaskCommand.Execute(second);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Equal("second-task-next-draft", workspace.ComposerText);
        var firstHistory = await fixture.Tasks.GetMessagesAsync(first.Id, fixture.Token);
        var secondHistory = await fixture.Tasks.GetMessagesAsync(second.Id, fixture.Token);
        Assert.Contains(firstHistory, message => message.Content == "first-task-queued");
        Assert.DoesNotContain(firstHistory, message => message.Content.Contains("second-task", StringComparison.Ordinal));
        Assert.Contains(secondHistory, message => message.Content == "second-task-queued");
        Assert.DoesNotContain(secondHistory, message => message.Content.Contains("first-task", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationAfterAtomicDispatchKeepsOneCommittedMessageAndDoesNotCallTheProvider()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var probe = new FollowupDispatchProbe(fixture.Followups);
        fixture.Workspace.ComposerText = string.Empty;
        await fixture.Workspace.ShutdownAsync();
        using var runs = new WorkspaceRunTracker();
        await using var workspace = fixture.CreateWorkspace(fixture.Tasks, runs, conversationDrafts: fixture.Drafts,
            followups: fixture.FollowupServices(probe));
        await workspace.LoadAsync();
        workspace.ComposerText = "first";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "committed-but-cancelled";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        var itemId = Assert.Single(workspace.Queue.Items).Id;
        probe.Dispatched = () => workspace.Queue.CancelPreparation(workspace.Catalog.SelectedTask!.Id);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.HasNotice);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
        Assert.Empty((await fixture.Followups.GetAsync(workspace.Catalog.SelectedTask!.Id, fixture.Token)).Items);
        Assert.Single(await fixture.Tasks.GetMessagesAsync(workspace.Catalog.SelectedTask.Id, fixture.Token), message => message.Id == itemId);
        Assert.Contains("消息已进入对话", workspace.Queue.Notice);
        await workspace.Queue.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(workspace.Queue.Items);
        Assert.False(workspace.Queue.ContinueCommand.CanExecute(null));
        await runs.StopAsync();
    }
}
