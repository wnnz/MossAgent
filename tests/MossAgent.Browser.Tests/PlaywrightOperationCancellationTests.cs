using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightOperationCancellationTests
{
    [Theory]
    [InlineData(false, "click")]
    [InlineData(true, "click")]
    [InlineData(false, "fill")]
    [InlineData(true, "fill")]
    [InlineData(false, "navigate")]
    [InlineData(true, "navigate")]
    [InlineData(false, "snapshot")]
    [InlineData(true, "snapshot")]
    public async Task CancellationClosesOnlyTheOwnedSessionAndRefusesReuse(bool borrowed, string operation)
    {
        await using var probe = new PlaywrightOperationCancellationProbe(borrowed);
        var session = probe.CreateSession();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = ExecuteAsync(session, operation, cancellation.Token);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(1, probe.RuntimeDisposals);
        Assert.Equal(borrowed ? 0 : 1, probe.ContextCloses);
        Assert.Equal(borrowed ? 1 : 0, probe.OwnedPageCloses);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ClickAsync("#another", TestContext.Current.CancellationToken));
        Assert.Equal(1, probe.Actions);
        await session.DisposeAsync();
        Assert.Equal(1, probe.RuntimeDisposals);
    }

    [Theory]
    [InlineData("click")]
    [InlineData("fill")]
    [InlineData("navigate")]
    [InlineData("snapshot")]
    public async Task AlreadyCancelledTokenCannotStartAnAction(string operation)
    {
        await using var probe = new PlaywrightOperationCancellationProbe(false);
        var session = probe.CreateSession();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteAsync(session, operation, cancellation.Token));
        Assert.Equal(0, probe.Actions);
        Assert.Equal(0, probe.RuntimeDisposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDoesNotReturnUntilOwnedCleanupCompletes(bool borrowed)
    {
        await using var probe = new PlaywrightOperationCancellationProbe(borrowed);
        var close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.CloseGate = close.Task;
        var session = probe.CreateSession();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = session.ClickAsync("#late", cancellation.Token);
        try
        {
            await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await probe.CloseStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(running.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ClickAsync("#another", TestContext.Current.CancellationToken));
            Assert.Equal(1, probe.Actions);
            close.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.Equal(1, probe.RuntimeDisposals);
        }
        finally { close.TrySetResult(); }
    }

    [Fact]
    public async Task CleanupFailureIsObservedAfterTheRuntimeAndPendingActionAreStopped()
    {
        await using var probe = new PlaywrightOperationCancellationProbe(false);
        probe.CloseFailureExpected = true;
        var close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.CloseGate = close.Task;
        var session = probe.CreateSession();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var running = session.ClickAsync("#late", cancellation.Token);
        await probe.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        close.SetException(new IOException("controlled close failure"));
        var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => running);
        Assert.Equal("browser_session_cleanup_failed", error.ErrorCode);
        Assert.True(error.HasSideEffects);
        Assert.Contains("任务浏览器关闭未获确认", error.Message);
        Assert.DoesNotContain("controlled close failure", error.Message);
        Assert.Equal(1, probe.RuntimeDisposals);
        await Assert.ThrowsAsync<IOException>(async () => await session.DisposeAsync());
    }

    [Fact]
    public async Task AnOrdinaryOperationFailureDoesNotMasqueradeAsCancellation()
    {
        await using var probe = new PlaywrightOperationCancellationProbe(false);
        var session = probe.CreateSession();
        var running = session.ClickAsync("#late", TestContext.Current.CancellationToken);
        probe.FailAction(new IOException("controlled action failure"));
        Assert.Equal("controlled action failure", (await Assert.ThrowsAsync<IOException>(() => running)).Message);
        Assert.Equal(0, probe.RuntimeDisposals);
    }

    private static Task ExecuteAsync(PlaywrightBrowserSession session, string operation, CancellationToken token) => operation switch
    {
        "click" => session.ClickAsync("#late", token),
        "fill" => session.TypeAsync("#late", "value", token),
        "navigate" => session.NavigateAsync(new Uri("http://127.0.0.1/owned"), token),
        "snapshot" => session.GetSnapshotAsync(token),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
