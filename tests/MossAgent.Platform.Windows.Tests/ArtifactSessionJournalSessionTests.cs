using System.Text;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ArtifactSessionJournalSessionTests
{
    [Fact]
    public async Task CreateRecordsTheSessionBeforeReturningIt()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator(), journal);

        var session = await factory.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token);

        var (taskId, fileName, fullPath, kind, bytes, _) = Assert.Single(journal.Records);
        Assert.Equal(fixture.Context.TaskId, taskId);
        Assert.Equal(Path.GetFileName(session.Path), fileName);
        Assert.Equal(session.Path, fullPath);
        Assert.Equal(ArtifactSessionKind.Artifact, kind);
        Assert.Null(bytes);
        session.Dispose();
    }

    [Fact]
    public async Task RecordFailureFailsTheCreateAndLeavesNoFileBehind()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal
        { RecordFailure = new IOException("synthetic journal outage") };
        var factory = new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator(), journal);

        await Assert.ThrowsAsync<IOException>(
            () => factory.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token));

        fixture.AssertNoArtifacts();
        Assert.Empty(journal.Marks);
    }

    [Fact]
    public async Task CompleteThenRetainMarksCompletedThenRetained()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator(), journal);
        var session = await factory.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token);
        await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("owned bytes"), fixture.Token);

        await session.CompleteAsync(fixture.Token);
        session.Retain();
        session.Dispose();

        var recordId = journal.Records[0].Id;
        Assert.Equal([
            (recordId, ArtifactSessionStage.Completed),
            (recordId, ArtifactSessionStage.Retained),
            (recordId, ArtifactSessionStage.Retained),
        ], journal.Marks.Select(mark => (mark.Id, mark.Stage)));
        Assert.Equal("owned bytes", await File.ReadAllTextAsync(session.Path, fixture.Token));
        Assert.Equal(11, journal.Marks[0].ByteCount);
    }

    [Fact]
    public async Task DisposeWithoutRetainMarksDiscarded()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator(), journal);
        var session = await factory.CreateAsync(fixture.Context, "preview", ".snapshot", fixture.Token);
        var path = session.Path;

        session.Dispose();

        var recordId = journal.Records[0].Id;
        Assert.Equal([(recordId, ArtifactSessionStage.Discarded)],
            journal.Marks.Select(mark => (mark.Id, mark.Stage)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task CleanupFailureMarksCleanupUnknownBeforeThrowing()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsArtifactFileSessionFactory(new WindowsAtomicFileWriteSessionFactory(),
            new WindowsAuthorizedDirectoryCreator(), journal);
        var session = await factory.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token);
        await session.CompleteAsync(fixture.Token);
        File.SetAttributes(session.Stream.SafeFileHandle, FileAttributes.ReadOnly);

        try
        {
            var error = Assert.Throws<FileMutationSessionException>(session.Dispose);
            Assert.Equal("artifact_cleanup_failed", error.ErrorCode);
            var recordId = journal.Records[0].Id;
            Assert.Equal([
                (recordId, ArtifactSessionStage.Completed),
                (recordId, ArtifactSessionStage.CleanupUnknown),
            ], journal.Marks.Select(mark => (mark.Id, mark.Stage)));
            Assert.Equal("artifact_cleanup_failed", journal.Marks[^1].ErrorCode);
            Assert.True(File.Exists(session.Path));
        }
        finally
        {
            File.SetAttributes(session.Path, FileAttributes.Normal);
            File.Delete(session.Path);
        }
    }

    [Fact]
    public async Task DiffInputRecordsKindAndByteCountThenDiscards()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator(), journal);
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var session = await factory.CreateAsync(fixture.Context, content, fixture.Token);

        var (taskId, fileName, fullPath, kind, bytes, _) = Assert.Single(journal.Records);
        Assert.Equal(fixture.Context.TaskId, taskId);
        Assert.Equal(Path.GetFileName(session.Path), fileName);
        Assert.Equal(session.Path, fullPath);
        Assert.Equal(ArtifactSessionKind.DiffInput, kind);
        Assert.Equal(5, bytes);
        var path = session.Path;
        session.Dispose();
        var recordId = journal.Records[0].Id;
        Assert.Equal([(recordId, ArtifactSessionStage.Discarded)],
            journal.Marks.Select(mark => (mark.Id, mark.Stage)));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DiffInputCleanupFailureMarksCleanupUnknownBeforeThrowing()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal();
        var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator(), journal);
        var session = await factory.CreateAsync(fixture.Context, new byte[] { 9 }, fixture.Token);
        File.SetAttributes(session.Path, FileAttributes.ReadOnly);

        try
        {
            var error = Assert.Throws<FileMutationSessionException>(session.Dispose);
            Assert.Equal("diff_input_cleanup_failed", error.ErrorCode);
            var recordId = journal.Records[0].Id;
            Assert.Equal([(recordId, ArtifactSessionStage.CleanupUnknown)],
                journal.Marks.Select(mark => (mark.Id, mark.Stage)));
            Assert.Equal("diff_input_cleanup_failed", journal.Marks[^1].ErrorCode);
        }
        finally
        {
            if (File.Exists(session.Path))
            {
                File.SetAttributes(session.Path, FileAttributes.Normal);
                File.Delete(session.Path);
            }
        }
    }

    [Fact]
    public async Task DiffInputRecordFailureFailsCreateAndLeavesNoFileBehind()
    {
        using var fixture = new OwnedArtifactFixture();
        var journal = new ObservedArtifactSessionJournal
        { RecordFailure = new IOException("synthetic journal outage") };
        var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator(), journal);

        await Assert.ThrowsAsync<IOException>(
            () => factory.CreateAsync(fixture.Context, new byte[] { 1 }, fixture.Token));

        Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactRoot));
    }
}
