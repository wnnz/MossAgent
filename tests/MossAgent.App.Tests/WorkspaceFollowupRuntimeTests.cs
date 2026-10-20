using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupRuntimeTests
{
    [Fact]
    public async Task QueuedMessagesUseTheirSavedModelAndReasoningWithoutConsumingTheNextDraft()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        Assert.True(workspace.Queue.IsAvailable);
        Assert.True(workspace.ModelPicker.TrySelect(fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id));
        workspace.ModelPicker.SelectedReasoning = "高";
        workspace.ComposerText = "先排队的第二条消息";
        Assert.True(workspace.Queue.EnqueueCommand.CanExecute(null));
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, workspace.ComposerText);
        Assert.Single(workspace.Queue.Items);
        workspace.ComposerText = "仍在编辑的下一份草稿";
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Equal("仍在编辑的下一份草稿", workspace.ComposerText);
        Assert.Empty(workspace.Queue.Items);
        var second = fixture.Server.Requests[1];
        Assert.Equal(fixture.AlternativeModel.ModelId, second.GetProperty("model").GetString());
        Assert.Equal("high", second.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("先排队的第二条消息", Readable(second));
        Assert.False(second.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        var task = Assert.Single(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
        Assert.Equal(taskId, task.Id);
        Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        var history = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
        Assert.Equal(2, history.Count(message => message.Role == MessageRole.User));
        Assert.Single(history, message => message.Content == "先排队的第二条消息");
    }

    [Fact]
    public async Task CancellationLeavesTheQueuePausedUntilExplicitContinuation()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        workspace.ComposerText = "取消后保留的消息";
        await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Cancelled, workspace.Catalog.SelectedTask?.Status);
        Assert.Single(workspace.Queue.Items);
        Assert.True(workspace.Queue.HasNotice);
        Assert.Single(fixture.Server.Requests);
        fixture.Server.ReleaseResponses();
        Assert.True(workspace.Queue.ContinueCommand.CanExecute(null));
        await workspace.Queue.ContinueCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Empty(workspace.Queue.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
    }

    [Fact]
    public async Task EditingOrderingAndRemovalChangeTheActualNextRequest()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var text in new[] { "原有第二条", "移动到首项", "需要移除的条目" })
        {
            workspace.ComposerText = text;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        await workspace.Queue.Items[1].MoveUpCommand.ExecuteAsync(null);
        Assert.Equal("移动到首项", workspace.Queue.Items[0].Prompt);
        workspace.Queue.Items[0].EditCommand.Execute(null);
        Assert.NotNull(workspace.Queue.Editor);
        workspace.Queue.Editor.EditText = "编辑后实际续发";
        await workspace.Queue.SaveEditCommand.ExecuteAsync(null);
        Assert.Null(workspace.Queue.Editor);
        await workspace.Queue.Items[2].DeleteCommand.ExecuteAsync(null);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !workspace.IsRunning);
        Assert.Contains("编辑后实际续发", Readable(fixture.Server.Requests[1]));
        Assert.DoesNotContain("需要移除的条目", Readable(fixture.Server.Requests[2]));
        Assert.Contains("原有第二条", Readable(fixture.Server.Requests[2]));
        Assert.Empty(workspace.Queue.Items);
    }

    private static string Readable(System.Text.Json.JsonElement request) => System.Text.Json.JsonSerializer.Serialize(request,
        new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
