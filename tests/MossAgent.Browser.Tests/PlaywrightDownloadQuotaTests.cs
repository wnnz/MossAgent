using MossAgent.Browser.Artifacts;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightDownloadQuotaTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string CreateTempFile(long length)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mossagent-dl-{Guid.NewGuid():N}.download");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.SetLength(length);
        return path;
    }

    private static void Grow(string path, long length)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        stream.SetLength(length);
    }

    [Fact]
    public async Task TempGrowingPastLimitMidFlightCancelsAndIsDiscarded()
    {
        var path = CreateTempFile(1024);
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            var taskId = Guid.NewGuid();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path, HoldFailure = true };
            var session = fixture.CreateSession(journal, taskId);
            await using var output = new MemoryStream();
            var pending = session.DownloadAsync("#download", output, Token);
            await fixture.Started.Task.WaitAsync(Token);
            // Bytes keep arriving after the wait has started; the watcher must cut them off.
            Grow(path, BrowserArtifactLimits.MaximumBytes + 1L);

            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => pending);

            Assert.Equal("browser_artifact_too_large", error.ErrorCode);
            Assert.False(error.HasSideEffects);
            Assert.Equal(0, fixture.SourceOpens);
            Assert.Equal(0, output.Length);
            Assert.Equal(2, fixture.Cancels); // quota cancel plus the cleanup retry
            Assert.Equal(1, fixture.Deletes);
            var record = Assert.Single(journal.Records);
            Assert.Equal(path, record.FullPath);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(record.Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.Discarded, mark.Stage);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TempAlreadyOversizedAtFirstPollStillCancels()
    {
        var path = CreateTempFile(BrowserArtifactLimits.MaximumBytes + 1L);
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path, HoldFailure = true };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();
            var pending = session.DownloadAsync("#download", output, Token);
            await fixture.Started.Task.WaitAsync(Token);

            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => pending);

            Assert.Equal("browser_artifact_too_large", error.ErrorCode);
            Assert.Equal(0, fixture.SourceOpens);
            Assert.Equal(2, fixture.Cancels);
            Assert.Equal(1, fixture.Deletes);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(ArtifactSessionStage.Discarded, mark.Stage);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UnresolvableTempPathStillBoundsTheArtifactStream()
    {
        await using var source = new RepeatingReadStream(BrowserArtifactLimits.MaximumBytes + 1024L);
        await using var fixture = new PlaywrightDownloadProbe { PathLookupFails = true, SourceOverride = source };
        var session = fixture.CreateSession();
        await using var output = new MemoryStream();

        var error = await Assert.ThrowsAsync<FileMutationSessionException>(
            () => session.DownloadAsync("#download", output, Token));

        Assert.Equal("browser_artifact_too_large", error.ErrorCode);
        Assert.Equal(BrowserArtifactLimits.MaximumBytes, output.Length);
        Assert.Equal(1, fixture.SourceOpens);
        Assert.False(source.CanRead);
        Assert.Equal(1, fixture.Cancels);
        Assert.Equal(1, fixture.Deletes);
    }

    [Fact]
    public async Task QuotaCancelWithFailedCleanupReportsCleanupUnknown()
    {
        var path = CreateTempFile(BrowserArtifactLimits.MaximumBytes + 1L);
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe
            { TempPath = path, HoldFailure = true, DeleteFails = true };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();
            var pending = session.DownloadAsync("#download", output, Token);
            await fixture.Started.Task.WaitAsync(Token);

            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => pending);

            Assert.Equal("browser_download_cleanup_failed", error.ErrorCode);
            Assert.True(error.HasSideEffects);
            var record = Assert.Single(journal.Records);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(record.Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.CleanupUnknown, mark.Stage);
            Assert.Equal("browser_download_cleanup_failed", mark.ErrorCode);
        }
        finally { File.Delete(path); }
    }
}
