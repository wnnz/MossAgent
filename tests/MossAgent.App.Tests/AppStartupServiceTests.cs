using System.Collections.Concurrent;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppStartupServiceTests
{
    [Fact]
    public async Task Loading_ReturnsImmediatelyAndPublishesCompletionOnlyOnTheUiQueue()
    {
        var manager = new DeferredStartupMcpManager();
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue);

        var loading = startup.StartAsync();
        Assert.Same(loading, startup.StartAsync());
        await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.True(state.IsLoading);
        Assert.False(state.IsReady);
        Assert.True(state.HasStatus);
        Assert.False(loading.IsCompleted);
        Assert.Null(manager.ReloadContext);
        Assert.Empty(queue);
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.False(state.IsReady);

        Assert.Single(queue)();

        Assert.Equal(1, manager.ReloadCount);
        Assert.True(state.IsReady);
        Assert.False(state.HasStatus);
        Assert.False(state.HasWarning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServerErrorsOrUnexpectedFailures_PublishSafeWarningsWithoutBlockingTheWorkspace(bool unexpected)
    {
        var manager = new DeferredStartupMcpManager();
        if (unexpected) manager.Failure = new InvalidOperationException("synthetic-startup-secret");
        else manager.Errors = new Dictionary<string, string> { ["fixture"] = "synthetic-startup-secret" };
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue);
        var loading = startup.StartAsync();
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Single(queue)();

        Assert.True(state.IsReady);
        Assert.True(state.HasWarning);
        Assert.True(state.HasStatus);
        Assert.DoesNotContain("synthetic-startup-secret", state.Status, StringComparison.Ordinal);
        Assert.Equal("查看 MCP 设置", state.OpenSettingsLabel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttachmentReclaimFailures_PublishStorageWarningWithoutBlockingTheWorkspace(bool unexpected)
    {
        var manager = new DeferredStartupMcpManager();
        var reaper = new RecordingAttachmentReaper();
        if (unexpected) reaper.Failure = new InvalidOperationException("synthetic-reclaim-secret");
        else reaper.Summary = new(4, 3, 0, 1);
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue, reaper: reaper);
        var loading = startup.StartAsync();
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Single(queue)();

        Assert.Equal(1, reaper.Invocations);
        Assert.True(state.IsReady);
        Assert.True(state.HasStatus);
        Assert.False(state.HasWarning);
        Assert.DoesNotContain("synthetic-reclaim-secret", state.Status, StringComparison.Ordinal);
        Assert.Equal("附件快照回收对账未完成，残留文件将保留待处理。", state.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArtifactJournalReconcile_PublishesStorageWarningOnlyOnFailure(bool fails)
    {
        var manager = new DeferredStartupMcpManager();
        var journal = new RecordingArtifactSessionJournal();
        if (fails) journal.ReconcileFailure = new InvalidOperationException("synthetic-reconcile-secret");
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue, artifactJournal: journal);
        var loading = startup.StartAsync();
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Single(queue)();

        Assert.Equal(1, journal.ReconcileCalls);
        Assert.True(state.IsReady);
        Assert.Equal(fails, state.HasStatus);
        Assert.False(state.HasWarning);
        if (fails) Assert.Equal("附件快照回收对账未完成，残留文件将保留待处理。", state.Status);
    }

    [Fact]
    public async Task SuccessfulReclaim_PublishesNoStorageWarning()
    {
        var manager = new DeferredStartupMcpManager();
        var reaper = new RecordingAttachmentReaper { Summary = new(7, 2, 5, 0) };
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue, reaper: reaper);
        var loading = startup.StartAsync();
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Single(queue)();

        Assert.Equal(1, reaper.Invocations);
        Assert.True(state.IsReady);
        Assert.False(state.HasStatus);
        Assert.False(state.HasWarning);
    }

    [Fact]
    public async Task ClosingDuringStartup_CancelsLoadingWithoutWaitingForTheUiQueueOrDisposingTheManager()
    {
        var manager = new DeferredStartupMcpManager();
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue);
        _ = startup.StartAsync();
        await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await startup.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.True(manager.WasCancelled);
        Assert.False(manager.IsDisposed);
        Assert.Empty(queue);
        Assert.False(state.IsReady);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => startup.StartAsync());
    }

    [Fact]
    public async Task QueuedCompletion_DoesNotMutateUiStateAfterDisposal()
    {
        var manager = new DeferredStartupMcpManager();
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        await using var startup = new AppStartupService(manager, state, queue.Enqueue);
        var loading = startup.StartAsync();
        manager.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        await startup.DisposeAsync();

        Assert.Single(queue)();

        Assert.False(state.IsReady);
        Assert.False(state.HasWarning);
    }

    [Fact]
    public async Task DisposingBeforeStart_DoesNotConnectToAnyServer()
    {
        var manager = new DeferredStartupMcpManager();
        var state = new AppStartupViewModel();
        var queue = new ConcurrentQueue<Action>();
        var startup = new AppStartupService(manager, state, queue.Enqueue);

        await startup.DisposeAsync();
        await startup.DisposeAsync();

        Assert.Equal(0, manager.ReloadCount);
        Assert.True(state.IsReady);
        Assert.Empty(queue);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => startup.StartAsync());
    }
}
