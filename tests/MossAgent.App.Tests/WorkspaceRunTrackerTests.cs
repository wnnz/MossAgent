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
        Assert.True(first.IsCancellationRequested);
        Assert.True(second.IsCancellationRequested);
    }

    [Fact]
    public void CancelAndRemoveCanRaceSafely()
    {
        using var tracker = new WorkspaceRunTracker();

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var state = CreateState($"task-{iteration}");
            tracker.Add(state);

            Parallel.Invoke(
                () => Parallel.For(0, 100, _ => tracker.Cancel(state.Task.Id)),
                () => tracker.Remove(state.Task.Id));

            Assert.False(tracker.IsRunning(state.Task.Id));
        }
    }

    [Fact]
    public void DisposedState_PreservesCancellationForFinalClassification()
    {
        using var state = CreateState("cancelled");
        state.Cancel();

        state.Dispose();

        Assert.True(state.IsCancellationRequested);
        Assert.True(state.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StopWaitsForEveryExecutionAndRejectsNewWork()
    {
        using var tracker = new WorkspaceRunTracker();
        var first = CreateState("first");
        var second = CreateState("second");
        tracker.Add(first);
        tracker.Add(second);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tracker.TrackExecution(() => firstRelease.Task);
        tracker.TrackExecution(() => secondRelease.Task);

        var stopping = tracker.StopAsync();

        Assert.True(first.IsCancellationRequested);
        Assert.True(second.IsCancellationRequested);
        Assert.False(stopping.IsCompleted);
        using var late = CreateState("late");
        Assert.Throws<ObjectDisposedException>(() => tracker.Add(late));
        Assert.Throws<ObjectDisposedException>(() => tracker.TrackExecution(() => Task.CompletedTask));
        firstRelease.SetResult();
        Assert.False(stopping.IsCompleted);
        secondRelease.SetResult();
        await stopping.WaitAsync(TestContext.Current.CancellationToken);
        await tracker.StopAsync();
    }

    [Fact]
    public async Task ThrowingCancellationStillCancelsOthersAndDrainsBeforeReportingFailure()
    {
        using var tracker = new WorkspaceRunTracker();
        var first = CreateState("first");
        var second = CreateState("second");
        tracker.Add(first);
        tracker.Add(second);
        using var registration = first.CancellationToken.Register(() => throw new InvalidOperationException("synthetic secret"));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tracker.TrackExecution(() => release.Task);

        var stopping = tracker.StopAsync();

        Assert.True(first.IsCancellationRequested);
        Assert.True(second.IsCancellationRequested);
        Assert.False(stopping.IsCompleted);
        release.SetResult();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => stopping);
        Assert.DoesNotContain("synthetic secret", failure.Message);
    }

    [Fact]
    public async Task CompletedExecutionFailureIsNotLostDuringShutdown()
    {
        using var tracker = new WorkspaceRunTracker();
        tracker.TrackExecution(() => throw new InvalidOperationException("synthetic secret"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => tracker.StopAsync());

        Assert.DoesNotContain("synthetic secret", failure.Message);
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
