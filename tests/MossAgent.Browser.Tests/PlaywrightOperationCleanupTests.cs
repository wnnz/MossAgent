using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightOperationCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedSessionCloseStillDrainsPendingAndPreservesSafeCleanupDetail(bool downloadCleanupFailed)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closes = 0;
        var operation = new PlaywrightBrowserOperation(() =>
        {
            closes++;
            pending.SetException(downloadCleanupFailed
                ? new FileMutationSessionException("safe download cleanup detail", "browser_download_cleanup_failed", true)
                : new IOException("private pending error"));
            return ValueTask.FromException(new IOException("private browser close error"));
        });
        var running = operation.RunAsync(() => pending.Task, cancellation.Token);
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => running);
        Assert.Equal(downloadCleanupFailed ? "browser_download_cleanup_failed" : "browser_session_cleanup_failed", error.ErrorCode);
        Assert.True(error.HasSideEffects);
        Assert.DoesNotContain("private", error.Message);
        Assert.True(pending.Task.IsCompleted);
        Assert.Equal(1, closes);
        if (downloadCleanupFailed) Assert.Contains("safe download cleanup detail", error.Message);
        Assert.Contains("任务浏览器关闭未获确认", error.Message);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => operation.RunAsync(() => Task.FromResult("unexpected"), TestContext.Current.CancellationToken));
    }
}
