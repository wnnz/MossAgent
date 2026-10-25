using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ArtifactSessionJournalTests
{
    private static AppDataPaths Paths(SqliteFailurePersistenceFixture fixture) =>
        new(fixture.Project.PrimaryDirectory);

    private static SqliteArtifactSessionJournal Journal(SqliteFailurePersistenceFixture fixture,
        TimeSpan? grace = null) =>
        new(fixture.Connections, new TaskArtifactPaths(Paths(fixture)), grace);

    private static string TaskDirectory(SqliteFailurePersistenceFixture fixture, Guid taskId) =>
        Path.Combine(Paths(fixture).ArtifactsPath, taskId.ToString("N"));

    private static async Task<long> SeedAsync(SqliteFailurePersistenceFixture fixture,
        SqliteArtifactSessionJournal journal, Guid taskId, ArtifactSessionStage stage,
        string? file, string? error = null, bool stale = true, ArtifactSessionKind kind = ArtifactSessionKind.Artifact)
    {
        var path = file ?? Path.Combine(TaskDirectory(fixture, taskId), $"seed-{Guid.NewGuid():N}.txt");
        var id = await journal.RecordAsync(taskId, Path.GetFileName(path), path, kind,
            kind == ArtifactSessionKind.DiffInput ? 4 : null, fixture.Token);
        await fixture.ExecuteAsync($"""
            UPDATE artifact_session_records SET stage={(int)stage},error_code={(error is null ? "NULL" : $"'{error}'")},
                updated_at={(stale ? "'2000-01-01T00:00:00.0000000+00:00'" : "updated_at")}
            WHERE id={id};
            """);
        return id;
    }

    [Fact]
    public async Task RecordThenAdvanceThroughStages()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var path = Path.Combine(TaskDirectory(fixture, fixture.Task.Id), "edit-1.txt");
        var id = await journal.RecordAsync(fixture.Task.Id, "edit-1.txt", path,
            ArtifactSessionKind.Artifact, null, fixture.Token);

        var recorded = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(id, recorded.Id);
        Assert.Equal(fixture.Task.Id, recorded.TaskId);
        Assert.Equal("edit-1.txt", recorded.FileName);
        Assert.Equal(path, recorded.FullPath);
        Assert.Equal(ArtifactSessionKind.Artifact, recorded.Kind);
        Assert.Equal(ArtifactSessionStage.Recorded, recorded.Stage);
        Assert.Null(recorded.ByteCount);
        Assert.Null(recorded.ErrorCode);

        await journal.MarkCompletedAsync(id, 42, fixture.Token);
        await journal.MarkRetainedAsync(id, fixture.Token);

        var retained = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(ArtifactSessionStage.Retained, retained.Stage);
        Assert.Equal(42, retained.ByteCount);
    }

    [Fact]
    public async Task CleanupUnknownThenDiscardedClearsErrorCode()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var id = await journal.RecordAsync(fixture.Task.Id, "edit-2.txt",
            Path.Combine(TaskDirectory(fixture, fixture.Task.Id), "edit-2.txt"),
            ArtifactSessionKind.Artifact, null, fixture.Token);

        await journal.MarkCleanupUnknownAsync(id, "artifact_cleanup_failed", fixture.Token);
        var unknown = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(ArtifactSessionStage.CleanupUnknown, unknown.Stage);
        Assert.Equal("artifact_cleanup_failed", unknown.ErrorCode);

        await journal.MarkDiscardedAsync(id, fixture.Token);
        var discarded = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(ArtifactSessionStage.Discarded, discarded.Stage);
        Assert.Null(discarded.ErrorCode);
    }

    [Fact]
    public async Task ReconcileClassifiesStaleRowsWithoutDeletingFiles()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var task = fixture.Task.Id;
        var directory = TaskDirectory(fixture, task);
        Directory.CreateDirectory(directory);

        async Task<long> PresentAsync(ArtifactSessionStage stage, string? error = null,
            ArtifactSessionKind kind = ArtifactSessionKind.Artifact)
        {
            var file = Path.Combine(directory, $"live-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(file, "leftover", fixture.Token);
            return await SeedAsync(fixture, journal, task, stage, file, error, kind: kind);
        }

        var staged = await PresentAsync(ArtifactSessionStage.Recorded);
        var completed = await PresentAsync(ArtifactSessionStage.Completed);
        var unknown = await PresentAsync(ArtifactSessionStage.CleanupUnknown, "artifact_cleanup_failed");
        var diffInput = await PresentAsync(ArtifactSessionStage.Recorded, kind: ArtifactSessionKind.DiffInput);
        var missing = await SeedAsync(fixture, journal, task, ArtifactSessionStage.Recorded,
            Path.Combine(directory, "gone.txt"));
        var foreign = Path.Combine(fixture.Project.PrimaryDirectory, $"foreign-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(foreign, "foreign", fixture.Token);
        var unverified = await SeedAsync(fixture, journal, task, ArtifactSessionStage.Recorded, foreign);
        var retained = await PresentAsync(ArtifactSessionStage.Retained);
        var fresh = await SeedAsync(fixture, journal, task, ArtifactSessionStage.Recorded, null, stale: false);

        await journal.ReconcileAsync(fixture.Token);

        var records = await journal.ReadTaskAsync(task, fixture.Token);
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, staged));
        Assert.Equal("staged", Error(records, staged));
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, completed));
        Assert.Equal("completed", Error(records, completed));
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, unknown));
        Assert.Equal("cleanup_unknown", Error(records, unknown));
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, diffInput));
        Assert.Equal(ArtifactSessionStage.Discarded, Stage(records, missing));
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, unverified));
        Assert.Equal("unverified", Error(records, unverified));
        Assert.True(File.Exists(foreign));
        Assert.Equal(ArtifactSessionStage.Retained, Stage(records, retained));
        Assert.Equal(ArtifactSessionStage.Recorded, Stage(records, fresh));

        var unresolved = await journal.ReadUnresolvedAsync(fixture.Project, fixture.Token);
        Assert.Equal([staged, completed, unknown, diffInput, unverified],
            unresolved.Select(record => record.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ReconcileClassifiesSdkTempRowsOutsideTaskDirectory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var tempDirectory = Path.Combine(fixture.Project.PrimaryDirectory, "sdk-temp");
        Directory.CreateDirectory(tempDirectory);
        var live = Path.Combine(tempDirectory, $"dl-{Guid.NewGuid():N}.download");
        await File.WriteAllTextAsync(live, "partial", fixture.Token);
        var held = Path.Combine(tempDirectory, $"dl-{Guid.NewGuid():N}.download");
        await File.WriteAllTextAsync(held, "partial", fixture.Token);
        var liveId = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.Recorded,
            live, kind: ArtifactSessionKind.ExternalSdkTemp);
        var heldId = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.CleanupUnknown,
            held, "browser_download_cleanup_failed", kind: ArtifactSessionKind.ExternalSdkTemp);
        var missingId = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.Recorded,
            Path.Combine(tempDirectory, "gone.download"), kind: ArtifactSessionKind.ExternalSdkTemp);

        await journal.ReconcileAsync(fixture.Token);

        var records = await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, liveId));
        Assert.Equal("sdk_temp", Error(records, liveId));
        Assert.Equal(ArtifactSessionStage.Orphaned, Stage(records, heldId));
        Assert.Equal("sdk_temp", Error(records, heldId));
        Assert.Equal(ArtifactSessionStage.Discarded, Stage(records, missingId));
        Assert.Null(Error(records, missingId));
        Assert.True(File.Exists(live));
        Assert.True(File.Exists(held));

        var unresolved = await journal.ReadUnresolvedAsync(fixture.Project, fixture.Token);
        Assert.Equal([liveId, heldId], unresolved.Select(record => record.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ReconcileRefusesPathsOutsideRecordedTaskDirectory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var otherTask = Guid.NewGuid();
        var otherDirectory = TaskDirectory(fixture, otherTask);
        Directory.CreateDirectory(otherDirectory);
        var sibling = Path.Combine(otherDirectory, "sibling.txt");
        await File.WriteAllTextAsync(sibling, "other task", fixture.Token);
        var id = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.Recorded, sibling);

        await journal.ReconcileAsync(fixture.Token);

        var record = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(ArtifactSessionStage.Orphaned, record.Stage);
        Assert.Equal("unverified", record.ErrorCode);
        Assert.True(File.Exists(sibling));
    }

    [Fact]
    public async Task AcknowledgeClosesOnlyOrphanedRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var directory = TaskDirectory(fixture, fixture.Task.Id);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "live-ack.txt");
        await File.WriteAllTextAsync(file, "leftover", fixture.Token);
        var orphaned = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.Recorded, file);
        var recorded = await SeedAsync(fixture, journal, fixture.Task.Id, ArtifactSessionStage.Recorded, null, stale: false);
        await journal.ReconcileAsync(fixture.Token);

        await Assert.ThrowsAsync<ArtifactSessionStaleException>(
            () => journal.AcknowledgeAsync(recorded, fixture.Token));
        await journal.AcknowledgeAsync(orphaned, fixture.Token);

        var records = await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(ArtifactSessionStage.Acknowledged, Stage(records, orphaned));
        Assert.Empty(await journal.ReadUnresolvedAsync(fixture.Project, fixture.Token));
        await Assert.ThrowsAsync<ArtifactSessionStaleException>(
            () => journal.AcknowledgeAsync(orphaned, fixture.Token));
    }

    [Fact]
    public async Task ReadUnresolvedScopesToOwningProject()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = Journal(fixture);
        var otherProject = new ProjectProfile(Guid.NewGuid(), "other",
            Path.Combine(fixture.Project.PrimaryDirectory, "other"), [], DateTimeOffset.UtcNow);
        await fixture.Repository.SaveProjectAsync(otherProject, fixture.Token);
        var now = DateTimeOffset.UtcNow;
        var otherTask = new AgentTask(Guid.NewGuid(), otherProject.Id, "other task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Failed, now, now, null);
        await fixture.Repository.SaveTaskAsync(otherTask, fixture.Token);

        foreach (var (task, stage) in new[] { (fixture.Task.Id, ArtifactSessionStage.Recorded), (otherTask.Id, ArtifactSessionStage.Completed) })
        {
            var directory = TaskDirectory(fixture, task);
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"live-{task:N}.txt");
            await File.WriteAllTextAsync(file, "leftover", fixture.Token);
            await SeedAsync(fixture, journal, task, stage, file);
        }
        await journal.ReconcileAsync(fixture.Token);

        var own = Assert.Single(await journal.ReadUnresolvedAsync(fixture.Project, fixture.Token));
        Assert.Equal(fixture.Task.Id, own.TaskId);
        var other = Assert.Single(await journal.ReadUnresolvedAsync(otherProject, fixture.Token));
        Assert.Equal(otherTask.Id, other.TaskId);
    }

    private static ArtifactSessionStage Stage(IReadOnlyList<ArtifactSessionRecord> records, long id) =>
        records.Single(record => record.Id == id).Stage;

    private static string? Error(IReadOnlyList<ArtifactSessionRecord> records, long id) =>
        records.Single(record => record.Id == id).ErrorCode;
}
