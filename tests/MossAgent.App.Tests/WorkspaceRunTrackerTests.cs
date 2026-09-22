using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceRunTrackerTests
{
    [Fact]
    public void TracksMessagesAndCancellationPerTask()
    {
        using var tracker = new WorkspaceRunTracker();
        var first = CreateState("first");
        var second = CreateState("second");
        tracker.Add(first);
        tracker.Add(second);

        first.Messages.Add(new ChatMessageViewModel("assistant", "one"));
        second.Messages.Add(new ChatMessageViewModel("assistant", "two"));
        tracker.Cancel(first.Task.Id);

        Assert.Equal("one", Assert.Single(tracker.GetMessages(first.Task.Id)!).Content);
        Assert.Equal("two", Assert.Single(tracker.GetMessages(second.Task.Id)!).Content);
        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
    }

    [Fact]
    public void RemoveDisposesOnlyRemovedRun()
    {
        using var tracker = new WorkspaceRunTracker();
        var first = CreateState("first");
        var second = CreateState("second");
        tracker.Add(first);
        tracker.Add(second);

        tracker.Remove(first.Task.Id);

        Assert.False(tracker.IsRunning(first.Task.Id));
        Assert.True(tracker.IsRunning(second.Task.Id));
        first.Cancel();
        second.Cancel();
        Assert.False(first.IsCancellationRequested);
        Assert.True(second.IsCancellationRequested);
    }

    [Fact]
    public void DisposeCancelsAndRemovesEveryRun()
    {
        var tracker = new WorkspaceRunTracker();
        var first = CreateState("first");
        var second = CreateState("second");
        tracker.Add(first);
        tracker.Add(second);

        tracker.Dispose();

        Assert.False(tracker.IsRunning(first.Task.Id));
        Assert.False(tracker.IsRunning(second.Task.Id));
        first.Cancel();
        second.Cancel();
        Assert.False(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
    }

    private static WorkspaceRunState CreateState(string title)
    {
        var projectId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var task = new AgentTask(
            Guid.NewGuid(), projectId, title, ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var project = new ProjectProfile(
            projectId, title, "C:\\workspace", ["C:\\workspace"], DateTimeOffset.UtcNow);
        var provider = new AiProvider(
            providerId, "provider", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test"), string.Empty, null, true, true,
            new Dictionary<string, string>());
        var model = new ModelProfile(
            Guid.NewGuid(), providerId, "model", "model", 128_000, 4_096,
            "medium", null, null, false, false, true, true, "{}");
        var assistant = new ChatMessageViewModel("assistant", string.Empty);
        return new WorkspaceRunState(
            task, project, provider, model, new ObservableCollection<ChatMessageViewModel>(), assistant);
    }
}
