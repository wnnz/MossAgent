using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DraftBackgroundPersistenceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public async Task PendingReadCompletesOutsideTheUiDispatchAndPreservesNewInput()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestConversationDraftRepository();
        SynchronizationContext? readContext = null;
        repository.ReadHandler = async (_, cancellationToken) =>
        {
            readContext = SynchronizationContext.Current;
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return null;
        };
        WorkspaceComposerDraftsViewModel? drafts = null;
        var loading = Task.CompletedTask;
        try
        {
            await _session.DispatchAsync(async () =>
            {
                drafts = new(repository, saveDelay: TimeSpan.FromMilliseconds(20));
                loading = drafts.LoadCurrentAsync();
                drafts.Text = "new input while the read is pending";
                await entered.Task.WaitAsync(Timeout, _token);
            }, _token);

            release.TrySetResult();
            await loading.WaitAsync(Timeout, _token);
            repository.ReadHandler = null;
            Assert.Null(readContext);
            Assert.Equal("new input while the read is pending", drafts!.Text);
            await drafts.StopAsync().WaitAsync(Timeout, _token);
            Assert.Equal(drafts.Text, (await repository.GetAsync(new(null, null), _token))?.Text);
        }
        finally
        {
            release.TrySetResult();
            repository.ReadHandler = null;
            await CleanupAsync(drafts);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSaveAndShutdownDoNotRequireTheUiLoop(bool failSave)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestConversationDraftRepository();
        SynchronizationContext? saveContext = null;
        repository.SaveHandler = async (updates, cancellationToken) =>
        {
            saveContext = SynchronizationContext.Current;
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            if (failSave) throw new IOException("synthetic storage failure");
            repository.SaveHandler = null;
            return await repository.SaveAsync(updates, cancellationToken);
        };
        WorkspaceComposerDraftsViewModel? drafts = null;
        var wrongNotificationThread = 0;
        try
        {
            await _session.DispatchAsync(async () =>
            {
                var uiThread = Environment.CurrentManagedThreadId;
                drafts = new(repository, saveDelay: TimeSpan.FromMilliseconds(20));
                drafts.PropertyChanged += (_, _) =>
                {
                    if (Environment.CurrentManagedThreadId != uiThread)
                        Interlocked.Exchange(ref wrongNotificationThread, 1);
                };
                await drafts.LoadCurrentAsync();
                drafts.Text = "pending save outside the UI loop";
                await entered.Task.WaitAsync(Timeout, _token);
            }, _token);

            var stopping = drafts!.StopAsync();
            release.TrySetResult();
            if (failSave)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => stopping.WaitAsync(Timeout, _token));
                Assert.True(drafts.HasNotice);
                Assert.Equal("pending save outside the UI loop", drafts.Text);
            }
            else
            {
                await stopping.WaitAsync(Timeout, _token);
                Assert.Equal(drafts.Text, (await repository.GetAsync(new(null, null), _token))?.Text);
            }
            Assert.Null(saveContext);
        }
        finally
        {
            release.TrySetResult();
            repository.SaveHandler = null;
            await CleanupAsync(drafts);
        }
        Assert.Equal(0, Volatile.Read(ref wrongNotificationThread));
    }

    [Fact]
    public async Task IgnoredRepositoryCancellationStillTimesOutWithoutTheUiLoop()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ConversationDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestConversationDraftRepository
        {
            ReadHandler = (_, _) => { entered.TrySetResult(); return release.Task; }
        };
        WorkspaceComposerDraftsViewModel? drafts = null;
        var loading = Task.CompletedTask;
        try
        {
            await _session.DispatchAsync(async () =>
            {
                drafts = new(repository, operationTimeout: TimeSpan.FromMilliseconds(250));
                loading = drafts.LoadCurrentAsync();
                await entered.Task.WaitAsync(Timeout, _token);
            }, _token);

            await loading.WaitAsync(Timeout, _token);
            Assert.True(drafts!.HasNotice);
            Assert.False(drafts.CanSend);
        }
        finally
        {
            release.TrySetResult(null);
            repository.ReadHandler = null;
            await CleanupAsync(drafts);
        }
    }

    private Task CleanupAsync(WorkspaceComposerDraftsViewModel? drafts) => _session.DispatchAsync(async () =>
    {
        if (drafts is null) return;
        try { await drafts.StopAsync(); }
        finally { drafts.Dispose(); }
    }, _token).WaitAsync(Timeout, _token);
}
