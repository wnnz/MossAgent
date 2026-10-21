using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightDownloadCleanupTests
{
    [Fact]
    public async Task SuccessReadsSdkStreamWithoutPathAccessAndDeletesDownloadObject()
    {
        await using var fixture = new PlaywrightDownloadProbe();
        var session = fixture.CreateSession();
        await using var output = new MemoryStream();
        var info = await session.DownloadAsync("#download", output, TestContext.Current.CancellationToken);
        Assert.Equal("sdk.txt", info.SuggestedFileName);
        Assert.Equal("sdk download", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(1, fixture.Clicks);
        Assert.Equal(1, fixture.SourceOpens);
        Assert.False(fixture.Source.CanRead);
        Assert.Equal(1, fixture.Deletes);
        Assert.Equal(0, fixture.Cancels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceOrDestinationFailureStillCancelsAndDeletesOriginalDownload(bool sourceFailure)
    {
        await using var fixture = new PlaywrightDownloadProbe { Failure = sourceFailure ? "private Authorization: secret" : null };
        var session = fixture.CreateSession();
        await using var output = new MemoryStream(new byte[1], writable: false);
        var exception = await Record.ExceptionAsync(() => session.DownloadAsync("#download", output, TestContext.Current.CancellationToken));
        Assert.NotNull(exception);
        Assert.DoesNotContain("secret", exception.Message);
        Assert.Equal(1, fixture.Cancels);
        Assert.Equal(1, fixture.Deletes);
        Assert.Equal(sourceFailure ? 0 : 1, fixture.SourceOpens);
        if (!sourceFailure) Assert.False(fixture.Source.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupFailureIsLocalizedAndNeverSkipsDeleteAfterFailedCancel(bool cancelFails)
    {
        await using var fixture = new PlaywrightDownloadProbe { CancelFails = cancelFails, DeleteFails = !cancelFails,
            Failure = cancelFails ? "download failed" : null };
        var session = fixture.CreateSession();
        await using var output = new MemoryStream();
        var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => session.DownloadAsync("#download", output, TestContext.Current.CancellationToken));
        Assert.Equal("browser_download_cleanup_failed", error.ErrorCode);
        Assert.True(error.HasSideEffects);
        Assert.DoesNotContain("private", error.Message);
        Assert.Contains("暂存清理未获确认", error.Message);
        Assert.Equal(1, fixture.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDrainsSdkCleanupAndPreservesKnownCleanupFailure(bool cleanupFails)
    {
        await using var fixture = new PlaywrightDownloadProbe { HoldFailure = true, DeleteFails = cleanupFails };
        var session = fixture.CreateSession();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var output = new MemoryStream();
        var pending = session.DownloadAsync("#download", output, cancellation.Token);
        await fixture.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        if (cleanupFails)
        {
            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => pending);
            Assert.Equal("browser_download_cleanup_failed", error.ErrorCode);
        }
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, fixture.RuntimeDisposals);
        Assert.Equal(1, fixture.Cancels);
        Assert.Equal(1, fixture.Deletes);
        Assert.Equal(0, fixture.SourceOpens);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.DownloadAsync("#download", output, TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Clicks);
    }
}
