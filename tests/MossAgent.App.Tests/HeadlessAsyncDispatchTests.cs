using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class HeadlessAsyncDispatchTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackFailure_IsReportedToTheAwaitingTest(bool asynchronous)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _session.DispatchAsync(async () =>
        {
            if (asynchronous) await Task.Yield();
            throw new InvalidOperationException("headless failure marker");
        }, TestContext.Current.CancellationToken));

        Assert.Equal("headless failure marker", error.Message);
        await _session.DispatchAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DelayedCallback_IsAwaitedUntilCompletion()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = false;
        var dispatched = _session.DispatchAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            completed = true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.False(dispatched.IsCompleted);
            Assert.False(completed);
        }
        finally { release.TrySetResult(); }

        await dispatched;
        Assert.True(completed);
    }

    [Fact]
    public async Task DelayedDraftFlushCompletesBeforeTheHeadlessCallbackReturns()
    {
        var repository = new TestConversationDraftRepository();
        var scope = new ConversationDraftScope(null, null);
        await _session.DispatchAsync(async () =>
        {
            using var drafts = new WorkspaceComposerDraftsViewModel(repository, saveDelay: TimeSpan.FromMilliseconds(50));
            await drafts.LoadCurrentAsync();
            drafts.Text = "pending headless draft";
            await drafts.StopAsync();
        }, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal("pending headless draft", (await repository.GetAsync(scope, TestContext.Current.CancellationToken))!.Text);
        await _session.DispatchAsync(() => Task.CompletedTask, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CallbackCancellation_IsNotMistakenForSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _session.DispatchAsync(
            () => Task.FromCanceled(cancellation.Token), TestContext.Current.CancellationToken));
    }
}
