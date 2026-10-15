using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppShutdownViewModelTests
{
    [Fact]
    public async Task PendingAndSuccessfulCleanup_AreSharedWithoutExecutingAgain()
    {
        var state = new AppShutdownViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Cleanup() { calls++; return release.Task; }

        var first = state.RunCleanupAsync(Cleanup);
        Assert.Same(first, state.RunCleanupAsync(Cleanup));
        Assert.Equal(1, calls);
        release.SetResult();
        await first.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Same(first, state.RunCleanupAsync(Cleanup));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledCleanup_CanBeExplicitlyRetried(bool cancelled)
    {
        var state = new AppShutdownViewModel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        var failed = cancelled ? Task.FromCanceled(cancellation.Token)
            : Task.FromException(new IOException("synthetic-private-diagnostic"));
        var first = state.RunCleanupAsync(() => failed);
        if (cancelled) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        else await Assert.ThrowsAsync<IOException>(() => first);

        var retry = state.RunCleanupAsync(() => Task.CompletedTask);

        Assert.NotSame(first, retry);
        await retry.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Same(retry, state.RunCleanupAsync(() => throw new InvalidOperationException("must not execute")));
        Assert.DoesNotContain("synthetic-private-diagnostic", state.Title + state.Message);
    }

    [Fact]
    public void ExitActions_AreAvailableOnlyAfterFailureAndDisableWhileRetrying()
    {
        var state = new AppShutdownViewModel();
        var retries = 0;
        var forced = 0;
        state.RetryExitRequested += () => retries++;
        state.ForceExitRequested += () => forced++;
        foreach (var command in new[] { state.RetryExitCommand, state.ForceExitCommand })
        {
            Assert.False(command.CanExecute(null));
            command.Execute(null);
        }
        state.Begin();
        Assert.False(state.RetryExitCommand.CanExecute(null));
        state.Fail();
        state.RetryExitCommand.Execute(null);
        state.ForceExitCommand.Execute(null);
        Assert.Equal(1, retries);
        Assert.Equal(1, forced);
        Assert.Contains("未保存", state.Message);
        state.Begin();
        state.RetryExitCommand.Execute(null);
        state.ForceExitCommand.Execute(null);
        Assert.Equal(1, retries);
        Assert.Equal(1, forced);
        state.Complete();
        Assert.False(state.IsVisible);
        Assert.False(state.RetryExitCommand.CanExecute(null));
    }
}
