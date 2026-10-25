using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentReclaimTests
{
    [Fact]
    public async Task OrphanMessageSnapshotOlderThanGraceIsReclaimedByHandle()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);
        Age(file);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.False(File.Exists(file));
        Assert.Equal(1, summary.Reclaimed);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM attachment_reclaim_intents WHERE stage=1;"));
    }

    [Fact]
    public async Task ReferencedMessageSnapshotIsKept()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "hi", DateTimeOffset.UtcNow);
        var snapshots = await new ConversationAttachmentStore(new TaskArtifactPaths(paths))
            .SaveAsync(fixture.Task.Id, message.Id, [Content()], fixture.Token);
        await fixture.Repository.AppendMessageAsync(message with { Attachments = snapshots }, fixture.Token);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, message.Id, Assert.Single(snapshots));
        Age(file);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.True(File.Exists(file));
        Assert.Equal(0, summary.Reclaimed);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task FollowupQueuedSnapshotIsKeptUntilPayloadIsRemoved()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var followupId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, followupId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, followupId, snapshot);
        var item = ConversationFollowupTestData.Create(fixture.Task) with
        { Id = followupId, Attachments = [snapshot] };
        await InsertFollowupAsync(fixture, item);
        Age(file);

        var kept = await Reaper(fixture).ReclaimAsync(fixture.Token);
        Assert.True(File.Exists(file));
        Assert.Equal(0, kept.Reclaimed);

        await fixture.ExecuteAsync(
            "DELETE FROM conversation_followups; DELETE FROM conversation_followup_queues;");
        var reclaimed = await Reaper(fixture).ReclaimAsync(fixture.Token);
        Assert.False(File.Exists(file));
        Assert.Equal(1, reclaimed.Reclaimed);
    }

    [Fact]
    public async Task LeasedOrphanSnapshotIsKeptUntilLeaseIsReleased()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);
        var leases = new AttachmentSnapshotLeases();
        Age(file);

        using (leases.Acquire(file))
        {
            var summary = await Reaper(fixture, leases).ReclaimAsync(fixture.Token);
            Assert.True(File.Exists(file));
            Assert.Equal(0, summary.Reclaimed);
        }

        var reclaimed = await Reaper(fixture, leases).ReclaimAsync(fixture.Token);
        Assert.False(File.Exists(file));
        Assert.Equal(1, reclaimed.Reclaimed);
    }

    [Fact]
    public async Task FreshOrphanInsideGracePeriodIsKept()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.True(File.Exists(file));
        Assert.Equal(0, summary.Reclaimed);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task OpenSnapshotHandleBlocksDeletionUntilClosed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);
        Age(file);

        await using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);
            Assert.True(File.Exists(file));
            Assert.Equal(0, summary.Reclaimed);
            Assert.Equal(1, summary.Failed);
            Assert.Equal(1, await fixture.CountAsync(
                "SELECT COUNT(*) FROM attachment_reclaim_intents WHERE stage=2 AND error_code='io';"));
        }

        var reclaimed = await Reaper(fixture).ReclaimAsync(fixture.Token);
        Assert.False(File.Exists(file));
        Assert.Equal(1, reclaimed.Reclaimed);
    }

    [Fact]
    public async Task JunctionCandidateIsRefusedAndOutsideContentSurvives()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Project.PrimaryDirectory, "outside"));
        var sentinel = Path.Combine(outside.FullName, Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(sentinel, "foreign", fixture.Token);
        Age(sentinel);
        var attachments = Directory.CreateDirectory(Path.Combine(
            paths.ArtifactsPath, fixture.Task.Id.ToString("N"), "attachments"));
        var link = Path.Combine(attachments.FullName, Guid.NewGuid().ToString("N"));
        await CreateJunctionAsync(link, outside.FullName, fixture.Token);
        try
        {
            var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);
            Assert.True(File.Exists(sentinel));
            Assert.Equal(0, summary.Scanned);
            Assert.Equal(0, summary.Reclaimed);
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
        }
    }

    [Fact]
    public async Task DraftDiscardFreesSnapshotAfterGracePeriod()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var drafts = ConversationDraftAttachmentTestData.Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = ConversationDraftAttachmentTestData.Attachment(fixture, "draft bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "text", 0, [attachment])], fixture.Token));
        var file = ConversationDraftAttachmentTestData.Snapshot(fixture, attachment);
        Age(file);

        var kept = await Reaper(fixture).ReclaimAsync(fixture.Token);
        Assert.True(File.Exists(file));
        Assert.Equal(0, kept.Reclaimed);

        Assert.True(await drafts.SaveAsync([new(scope, "text", 1)], fixture.Token));
        var reclaimed = await Reaper(fixture).ReclaimAsync(fixture.Token);
        Assert.False(File.Exists(file));
        Assert.Equal(1, reclaimed.Reclaimed);
    }

    [Fact]
    public async Task SteeringReferencedDraftSnapshotIsKeptAfterDraftConsumption()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var paths = Paths(fixture.Inner);
        var repository = new SqliteConversationSteeringRepository(fixture.Connections, paths: paths);
        var drafts = ConversationDraftAttachmentTestData.Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = ConversationDraftAttachmentTestData.Attachment(fixture.Inner, "steered bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "text", 0, [attachment])], fixture.Token));
        var file = ConversationDraftAttachmentTestData.Snapshot(fixture.Inner, attachment);
        var run = await fixture.OpenAsync();
        Assert.NotNull(await repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "text",
            new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        Age(file);

        var kept = await Reaper(fixture.Inner).ReclaimAsync(fixture.Token);
        Assert.True(File.Exists(file));
        Assert.Equal(0, kept.Reclaimed);

        await fixture.ExecuteAsync("DELETE FROM conversation_steering_input_attachments;");
        var reclaimed = await Reaper(fixture.Inner).ReclaimAsync(fixture.Token);
        Assert.False(File.Exists(file));
        Assert.Equal(1, reclaimed.Reclaimed);
    }

    [Fact]
    public async Task RolledBackSteeringAcceptanceLeavesAnOrphanedSnapshotEligibleForReclaim()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var paths = Paths(fixture.Inner);
        var repository = new SqliteConversationSteeringRepository(fixture.Connections, paths: paths);
        var drafts = ConversationDraftAttachmentTestData.Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var kept = ConversationDraftAttachmentTestData.Attachment(fixture.Inner, "retained draft bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "text", 0, [kept])], fixture.Token));
        var keptFile = ConversationDraftAttachmentTestData.Snapshot(fixture.Inner, kept);
        var run = await fixture.OpenAsync();
        var rolledBack = ConversationDraftAttachmentTestData.Attachment(fixture.Inner, "rolled-back steering bytes");
        await Assert.ThrowsAsync<ConversationDraftConflictException>(() => repository.AcceptAsync(run.TaskId, run.Id,
            Guid.NewGuid(), "text", new ConversationDraftUpdate(scope, string.Empty, 0, [rolledBack]), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
        var orphaned = ConversationDraftAttachmentTestData.Snapshot(fixture.Inner, rolledBack);
        Age(keptFile);
        Age(orphaned);

        var summary = await Reaper(fixture.Inner).ReclaimAsync(fixture.Token);

        Assert.True(File.Exists(keptFile));
        Assert.False(File.Exists(orphaned));
        Assert.Equal(1, summary.Reclaimed);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task TemporaryWriteLeftoverIsReclaimed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var directory = Directory.CreateDirectory(Path.Combine(paths.ArtifactsPath,
            fixture.Task.Id.ToString("N"), "attachments", Guid.NewGuid().ToString("N")));
        var leftover = Path.Combine(directory.FullName,
            Guid.NewGuid().ToString("N") + ".txt." + Guid.NewGuid().ToString("N") + ".tmp");
        await File.WriteAllTextAsync(leftover, "partial", fixture.Token);
        Age(leftover);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.False(File.Exists(leftover));
        Assert.Equal(1, summary.Reclaimed);
        Assert.False(Directory.Exists(directory.FullName));
    }

    [Fact]
    public async Task InterruptedPassResumesAndClosesStaleIntents()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);
        var vanished = Path.Combine(Path.GetDirectoryName(file)!, Guid.NewGuid().ToString("N") + ".txt");
        await InsertIntentAsync(fixture, file, stage: 2);
        await InsertIntentAsync(fixture, vanished, stage: 0);
        Age(file);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.False(File.Exists(file));
        Assert.Equal(1, summary.Reclaimed);
        Assert.Equal(2, await fixture.CountAsync(
            "SELECT COUNT(*) FROM attachment_reclaim_intents WHERE stage=1;"));
    }

    [Fact]
    public async Task ToolArtifactRowForSnapshotPathKeepsTheFile()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Paths(fixture);
        var messageId = Guid.NewGuid();
        var snapshot = await WriteOrphanAsync(fixture, paths, messageId);
        var file = MessageSnapshotPath(paths, fixture.Task.Id, messageId, snapshot);
        await InsertToolArtifactAsync(fixture, file);
        Age(file);

        var summary = await Reaper(fixture).ReclaimAsync(fixture.Token);

        Assert.True(File.Exists(file));
        Assert.Equal(0, summary.Reclaimed);
    }

    private static AppDataPaths Paths(SqliteFailurePersistenceFixture fixture) =>
        new(fixture.Project.PrimaryDirectory);

    private static AttachmentSnapshotReaper Reaper(SqliteFailurePersistenceFixture fixture,
        AttachmentSnapshotLeases? leases = null) =>
        new(fixture.Connections, Paths(fixture), leases, gracePeriod: TimeSpan.FromHours(24));

    private static WorkspaceAttachmentContent Content() => new("note.txt", "note.txt", "payload", 7);

    private static async Task<ConversationAttachment> WriteOrphanAsync(
        SqliteFailurePersistenceFixture fixture, AppDataPaths paths, Guid containerId)
    {
        var snapshots = await new ConversationAttachmentStore(new TaskArtifactPaths(paths))
            .SaveAsync(fixture.Task.Id, containerId, [Content()], fixture.Token);
        return Assert.Single(snapshots);
    }

    private static string MessageSnapshotPath(AppDataPaths paths, Guid taskId, Guid messageId,
        ConversationAttachment attachment) => Path.Combine(paths.ArtifactsPath, taskId.ToString("N"),
        "attachments", messageId.ToString("N"), attachment.Id.ToString("N") + ".txt");

    private static void Age(string path) =>
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - TimeSpan.FromDays(2));

    private static async Task InsertFollowupAsync(SqliteFailurePersistenceFixture fixture, ConversationFollowup item)
    {
        await using var connection = fixture.Connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO conversation_followup_queues(task_id,revision) VALUES($task,1);
            INSERT INTO conversation_followups(id,task_id,ordinal,payload) VALUES($id,$task,0,$payload);
            """;
        command.Parameters.AddWithValue("$task", fixture.Task.Id.ToString("D"));
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(item));
        await command.ExecuteNonQueryAsync(fixture.Token);
    }

    private static async Task InsertIntentAsync(SqliteFailurePersistenceFixture fixture, string path, int stage)
    {
        await using var connection = fixture.Connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO attachment_reclaim_intents(path,byte_count,stage,error_code,created_at,updated_at)
            VALUES($path,7,$stage,NULL,$now,$now);
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$stage", stage);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(fixture.Token);
    }

    private static async Task InsertToolArtifactAsync(SqliteFailurePersistenceFixture fixture, string path)
    {
        await using var connection = fixture.Connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tool_executions(id,task_id,call_id,tool_name,approval_policy,started_at)
            VALUES($execution,$task,$call,'file_read',2,$now);
            INSERT INTO tool_artifacts(id,execution_id,task_id,name,path,media_type,length,sha256,created_at)
            VALUES($artifact,$execution,$task,'orphan.txt',$path,'text/plain',7,$hash,$now);
            """;
        command.Parameters.AddWithValue("$execution", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$artifact", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$task", fixture.Task.Id.ToString("D"));
        command.Parameters.AddWithValue("$call", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$hash", new string('0', 64));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(fixture.Token);
    }

    private static async Task CreateJunctionAsync(string link, string target, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        var startInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) startInfo.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Junction fixture failed to start.");
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0,
            $"Fixture junction failed: {await process.StandardOutput.ReadToEndAsync(token)} {await process.StandardError.ReadToEndAsync(token)}");
    }
}
