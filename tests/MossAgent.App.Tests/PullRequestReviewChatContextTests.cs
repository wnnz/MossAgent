using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestReviewChatContextTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LongSavedHistoryCannotEvictTheFirstSnapshotFromTheActualProviderRequest() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.Page.Inspection.Files.LoadCommand.ExecuteAsync(null);
        var task = await fixture.OpenAsync();
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        await SetContextLengthAsync(fixture, 4096);
        var now = DateTimeOffset.UtcNow;
        await fixture.Tasks.SaveTaskWithMessagesAsync(task,
            [new(Guid.NewGuid(), task.Id, MessageRole.User, new string('h', 40000), now),
             new(Guid.NewGuid(), task.Id, MessageRole.Assistant, new string('a', 40000), now.AddMilliseconds(1))], fixture.Token);
        var sourceRequests = fixture.Source.Transport.RequestCount;

        await fixture.SendAsync("current review question");
        await fixture.SendAsync("follow-up review question");

        foreach (var body in fixture.Transport.Requests)
        {
            AssertSnapshot(body, binding.Context);
            Assert.DoesNotContain(new string('h', 40000), body, StringComparison.Ordinal);
        }
        Assert.Equal(2, fixture.Transport.Requests.Count);
        Assert.Contains("current review question", fixture.Transport.Requests.Last(), StringComparison.Ordinal);
        Assert.Contains("follow-up review question", fixture.Transport.Requests.Last(), StringComparison.Ordinal);
        Assert.Equal(sourceRequests, fixture.Source.Transport.RequestCount);
        var saved = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal(6, saved.Count);
        Assert.DoesNotContain(saved, message => message.Content.Contains("Untrusted PR snapshot", StringComparison.Ordinal));
        Assert.Equal(binding.Context, (await fixture.Chats.GetAsync(task.Id, fixture.Token))?.Context);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InsufficientCapacityNeverCallsProviderAndConfigurationRepairRequiresManualSend() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestReviewChatFixture.CreateAsync();
        await fixture.Page.Inspection.Files.LoadCommand.ExecuteAsync(null);
        var task = await fixture.OpenAsync();
        var binding = (await fixture.Chats.GetAsync(task.Id, fixture.Token))!;
        Assert.True(binding.Context.Length > 512);
        await SetContextLengthAsync(fixture, 1024);
        fixture.Workspace.ComposerText = "review with insufficient capacity";
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && fixture.Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Failed);

        Assert.Empty(fixture.Transport.Requests);
        Assert.Contains(WorkspaceText.Get("TaskFailureModelContextCapacity"), fixture.Workspace.Activity, StringComparison.Ordinal);
        Assert.Equal(WorkspaceText.Get("TaskFailureModelContextCapacity"), fixture.Workspace.TaskFailureHint);
        var failedMessages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Equal("review with insufficient capacity", Assert.Single(failedMessages).Content);
        Assert.Equal(TaskFailureReason.ModelContextCapacity, (await fixture.Tasks.GetTasksAsync(null, fixture.Token)).Single(item => item.Id == task.Id).FailureReason);
        await fixture.Workspace.LoadAsync();
        Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(WorkspaceText.Get("TaskFailureModelContextCapacity"), fixture.Workspace.TaskFailureHint);
        await SetContextLengthAsync(fixture, 32768);
        Assert.Empty(fixture.Transport.Requests);
        Assert.False(fixture.Workspace.IsRunning);

        await fixture.SendAsync("explicit retry after repairing capacity");

        AssertSnapshot(Assert.Single(fixture.Transport.Requests), binding.Context);
        Assert.Equal(3, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count);
        Assert.Null((await fixture.Tasks.GetTasksAsync(null, fixture.Token)).Single(item => item.Id == task.Id).FailureReason);
    }, TestContext.Current.CancellationToken);

    private static async Task SetContextLengthAsync(PullRequestReviewChatFixture fixture, int contextLength)
    {
        var model = fixture.Workspace.ModelPicker.SelectedItem!.Model with { ContextLength = contextLength };
        await fixture.Source.Configurations.SaveModelAsync(model, fixture.Token);
        await fixture.Workspace.ModelPicker.RefreshCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => fixture.Workspace.ModelPicker.CanSendWithSelectedModel
            && fixture.Workspace.ModelPicker.SelectedItem?.Model.ContextLength == contextLength && fixture.Workspace.Drafts.CanSend);
    }

    private static void AssertSnapshot(string body, string snapshot)
    {
        using var document = JsonDocument.Parse(body);
        var input = document.RootElement.GetProperty("input").EnumerateArray().ToArray();
        var snapshots = input.Where(item => item.GetProperty("role").GetString() == "user"
            && item.GetProperty("content").GetString() == snapshot).ToArray();
        Assert.Single(snapshots);
        Assert.All(input.Where(item => item.GetProperty("role").GetString() == "system"),
            item => Assert.DoesNotContain("Untrusted PR snapshot", item.GetRawText(), StringComparison.Ordinal));
    }
}
