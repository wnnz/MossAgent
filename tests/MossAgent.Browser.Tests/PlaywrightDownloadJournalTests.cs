using MossAgent.Browser.Artifacts;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightDownloadJournalTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string CreateTempFile(long? length = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mossagent-dl-{Guid.NewGuid():N}.download");
        if (length is { } size)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.SetLength(size);
        }
        else File.WriteAllText(path, "sdk download");
        return path;
    }

    [Fact]
    public async Task SuccessRecordsSdkTempThenMarksDiscarded()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            var taskId = Guid.NewGuid();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path };
            var session = fixture.CreateSession(journal, taskId);
            await using var output = new MemoryStream();

            var info = await session.DownloadAsync("#download", output, Token);

            Assert.Equal("sdk.txt", info.SuggestedFileName);
            Assert.Equal("sdk download", System.Text.Encoding.UTF8.GetString(output.ToArray()));
            var record = Assert.Single(journal.Records);
            Assert.Equal(taskId, record.TaskId);
            Assert.Equal(Path.GetFileName(path), record.FileName);
            Assert.Equal(path, record.FullPath);
            Assert.Equal(ArtifactSessionKind.ExternalSdkTemp, record.Kind);
            Assert.Null(record.ByteCount);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(record.Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.Discarded, mark.Stage);
            Assert.Equal(1, fixture.Deletes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task OversizedSdkTempThrowsBeforeStreamingAndIsDiscarded()
    {
        var path = CreateTempFile(BrowserArtifactLimits.MaximumBytes + 1L);
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();

            var error = await Assert.ThrowsAsync<FileMutationSessionException>(
                () => session.DownloadAsync("#download", output, Token));

            Assert.Equal("browser_artifact_too_large", error.ErrorCode);
            Assert.False(error.HasSideEffects);
            Assert.Equal(0, fixture.SourceOpens);
            Assert.Equal(1, fixture.Cancels);
            Assert.Equal(1, fixture.Deletes);
            var record = Assert.Single(journal.Records);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(record.Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.Discarded, mark.Stage);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task JournalRecordFailureCancelsAndDeletesBeforeStreaming()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal
            { RecordFailure = new IOException("journal offline") };
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();

            var error = await Assert.ThrowsAsync<IOException>(
                () => session.DownloadAsync("#download", output, Token));

            Assert.Equal("journal offline", error.Message);
            Assert.Equal(0, fixture.SourceOpens);
            Assert.Equal(1, fixture.Cancels);
            Assert.Equal(1, fixture.Deletes);
            Assert.Empty(journal.Marks);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CleanupFailureMarksCleanupUnknownBeforeThrowing()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path, DeleteFails = true };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();

            var error = await Assert.ThrowsAsync<FileMutationSessionException>(
                () => session.DownloadAsync("#download", output, Token));

            Assert.Equal("browser_download_cleanup_failed", error.ErrorCode);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(journal.Records[0].Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.CleanupUnknown, mark.Stage);
            Assert.Equal("browser_download_cleanup_failed", mark.ErrorCode);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvableSdkPathSkipsRecording(bool pathLookupFails)
    {
        var journal = new RecordingArtifactSessionJournal();
        await using var fixture = new PlaywrightDownloadProbe { PathLookupFails = pathLookupFails };
        var session = fixture.CreateSession(journal, Guid.NewGuid());
        await using var output = new MemoryStream();

        var info = await session.DownloadAsync("#download", output, Token);

        Assert.Equal("sdk.txt", info.SuggestedFileName);
        Assert.Equal("sdk download", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        Assert.Empty(journal.Records);
        Assert.Empty(journal.Marks);
        Assert.Equal(1, fixture.Deletes);
    }

    [Fact]
    public async Task CancellationAfterRecordMarksDiscardedWhenDeleteSucceeds()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path, HoldSource = true };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            await using var output = new MemoryStream();
            var pending = session.DownloadAsync("#download", output, cancellation.Token);
            await fixture.StreamRequested.Task.WaitAsync(Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

            var record = Assert.Single(journal.Records);
            var mark = Assert.Single(journal.Marks);
            Assert.Equal(record.Id, mark.Id);
            Assert.Equal(ArtifactSessionStage.Discarded, mark.Stage);
            Assert.Equal(1, fixture.Cancels);
            Assert.Equal(1, fixture.Deletes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MarkFailureNeverMasksSuccessfulTransfer()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal
            { MarkFailure = new IOException("marks offline") };
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path };
            var session = fixture.CreateSession(journal, Guid.NewGuid());
            await using var output = new MemoryStream();

            var info = await session.DownloadAsync("#download", output, Token);

            Assert.Equal("sdk.txt", info.SuggestedFileName);
            Assert.Single(journal.Records);
            Assert.Equal(1, fixture.Deletes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MissingTaskIdSkipsRecordingButStillChecksQuota()
    {
        var path = CreateTempFile();
        try
        {
            var journal = new RecordingArtifactSessionJournal();
            await using var fixture = new PlaywrightDownloadProbe { TempPath = path };
            var session = fixture.CreateSession(journal, taskId: null);
            await using var output = new MemoryStream();

            var info = await session.DownloadAsync("#download", output, Token);

            Assert.Equal("sdk.txt", info.SuggestedFileName);
            Assert.Empty(journal.Records);
            Assert.Empty(journal.Marks);
        }
        finally { File.Delete(path); }
    }
}
