using MossAgent.Browser.Artifacts;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class DownloadQuotaWatcherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task ResolvedOversizedTempCancelsOnceAndFlagsQuota()
    {
        var cancels = 0;
        var watcher = new DownloadQuotaWatcher(
            () => Task.FromResult<string?>("sdk.temp"), _ => 128L,
            () => { cancels++; return Task.CompletedTask; },
            () => false, Tick, 64);

        await watcher.RunAsync(Token);

        Assert.True(watcher.QuotaExceeded);
        Assert.Equal("sdk.temp", watcher.ResolvedPath);
        Assert.Equal(1, cancels);
    }

    [Fact]
    public async Task PathNotYetAssignedKeepsPollingUntilItResolves()
    {
        var attempts = 0;
        var cancels = 0;
        var watcher = new DownloadQuotaWatcher(
            () => ++attempts <= 2
                ? Task.FromException<string?>(new IOException("path not yet assigned"))
                : Task.FromResult<string?>("sdk.temp"),
            _ => 128L,
            () => { cancels++; return Task.CompletedTask; },
            () => false, Tick, 64);

        await watcher.RunAsync(Token);

        Assert.True(watcher.QuotaExceeded);
        Assert.Equal(3, attempts);
        Assert.Equal(1, cancels);
    }

    [Fact]
    public async Task TempBelowLimitNeverCancels()
    {
        var checks = 0;
        var cancels = 0;
        var watcher = new DownloadQuotaWatcher(
            () => Task.FromResult<string?>("sdk.temp"), _ => 64L,
            () => { cancels++; return Task.CompletedTask; },
            () => ++checks > 3, Tick, 64);

        await watcher.RunAsync(Token);

        Assert.False(watcher.QuotaExceeded);
        Assert.Equal(0, cancels);
    }

    [Fact]
    public async Task DownloadWaitCompletingBeforePathResolvesSkipsCancel()
    {
        var checks = 0;
        var cancels = 0;
        var unresolved = new TaskCompletionSource<string?>();
        var watcher = new DownloadQuotaWatcher(
            () => unresolved.Task, _ => 128L,
            () => { cancels++; return Task.CompletedTask; },
            () => ++checks > 2, Tick, 64);

        await watcher.RunAsync(Token);

        Assert.False(watcher.QuotaExceeded);
        Assert.Null(watcher.ResolvedPath);
        Assert.Equal(0, cancels);
    }

    [Fact]
    public async Task LostCancelStillFlagsQuotaSoTheCallerCanRefuse()
    {
        var cancels = 0;
        var watcher = new DownloadQuotaWatcher(
            () => Task.FromResult<string?>("sdk.temp"), _ => 128L,
            () => { cancels++; return Task.FromException(new IOException("cancel lost")); },
            () => false, Tick, 64);

        await watcher.RunAsync(Token);

        Assert.True(watcher.QuotaExceeded);
        Assert.Equal(1, cancels);
    }

    [Fact]
    public async Task CancellationStopsPolling()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var watcher = new DownloadQuotaWatcher(
            () => Task.FromResult<string?>("sdk.temp"), _ => 64L,
            () => Task.CompletedTask, () => false, Tick, 64);
        var running = watcher.RunAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

        Assert.False(watcher.QuotaExceeded);
    }
}
