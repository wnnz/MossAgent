using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceReviewReadModePersistenceTests
{
    [Fact]
    public async Task ModesHaveDistinctTasksAndIndependentFirstSnapshotsAcrossReopen()
    {
        await using var fixture = await SourceReviewModeTestFixture.CreateAsync();
        var snapshot = await fixture.Chats.OpenAsync(fixture.Binding, "snapshot", fixture.Token);
        var connected = fixture.Binding with { TaskId = Guid.NewGuid(), Mode = SourceReviewChatMode.ReadOnlySource, Context = "connected first snapshot" };
        var task = await fixture.Chats.OpenAsync(connected, "connected", fixture.Token);
        Assert.NotEqual(snapshot.Id, task.Id);
        var reopened = new SqliteSourceReviewChats(fixture.Storage.Connections);

        var same = await reopened.OpenAsync(connected with { TaskId = Guid.NewGuid(), Context = "replacement",
            Revision = connected.Revision with { HeadRevision = connected.Revision.HeadRevision.ToUpperInvariant() } }, "ignored", fixture.Token);

        Assert.Equal(task.Id, same.Id);
        Assert.Equal(connected, await reopened.GetAsync(task.Id, fixture.Token));
        Assert.Equal(fixture.Binding, await reopened.GetAsync(snapshot.Id, fixture.Token));
        Assert.Equal(2, (await fixture.Storage.Repository.GetTasksAsync(null, fixture.Token)).Count);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Storage.Repository.SaveTaskAsync(task with { ApprovalPolicy = ApprovalPolicy.FullAccess }, fixture.Token));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Storage.Repository.SaveTaskAsync(task with { WorktreePath = fixture.Storage.Project.PrimaryDirectory }, fixture.Token));
    }

    [Fact]
    public async Task Version21MigrationPreservesOldSnapshotMessagesAndNeverEnablesReads()
    {
        await using var fixture = await SourceReviewModeTestFixture.CreateAsync();
        var task = await fixture.Chats.OpenAsync(fixture.Binding, "snapshot", fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "preserved prompt", DateTimeOffset.UtcNow);
        await fixture.Storage.Repository.SaveTaskWithMessagesAsync(task, [message], fixture.Token);
        await fixture.Storage.Repository.SetTaskPinnedAsync(task.Id, true, fixture.Token);
        await fixture.RestoreVersion21Async();

        await fixture.Storage.Initializer.InitializeAsync(fixture.Token);
        await fixture.Storage.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(30, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Binding, await new SqliteSourceReviewChats(fixture.Storage.Connections).GetAsync(task.Id, fixture.Token));
        Assert.Equal(message, Assert.Single(await fixture.Storage.Repository.GetMessagesAsync(task.Id, fixture.Token)));
        Assert.Equal(task.Id, Assert.Single(await fixture.Storage.Repository.GetPinnedTasksAsync(fixture.Token)).Id);
        Assert.Equal(0, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedModeMigrationRestoresLegacyTableAndNeedsExplicitRetry()
    {
        await using var fixture = await SourceReviewModeTestFixture.CreateAsync();
        await fixture.Chats.OpenAsync(fixture.Binding, "snapshot", fixture.Token);
        await fixture.RestoreVersion21Async();
        await fixture.Storage.ExecuteAsync("""
            CREATE TRIGGER reject_mode_version BEFORE INSERT ON schema_versions
            WHEN NEW.version=22 BEGIN SELECT RAISE(ABORT,'synthetic mode migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Storage.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(21, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM pragma_table_info('source_review_chats') WHERE name='mode';"));
        Assert.Equal(1, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM source_review_chats;"));
        Assert.Equal(1, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name='source_review_chat_environment';"));
        Assert.Equal(0, await fixture.Storage.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='source_review_chats_v22';"));
        await fixture.Storage.ExecuteAsync("DROP TRIGGER reject_mode_version;");
        await fixture.Storage.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(fixture.Binding, await fixture.Chats.GetAsync(fixture.Binding.TaskId, fixture.Token));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task InvalidModesAreRejectedByDomainAndDatabase(int mode)
    {
        await using var fixture = await SourceReviewModeTestFixture.CreateAsync();
        await Assert.ThrowsAsync<SourceReviewException>(() => fixture.Chats.OpenAsync(fixture.Binding with { Mode = (SourceReviewChatMode)mode }, "invalid", fixture.Token));
        var task = await fixture.Chats.OpenAsync(fixture.Binding, "snapshot", fixture.Token);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Storage.ExecuteAsync($"UPDATE source_review_chats SET mode={mode} WHERE task_id='{task.Id}';"));
        Assert.Equal(SourceReviewChatMode.Snapshot, (await fixture.Chats.GetAsync(task.Id, fixture.Token))!.Mode);
    }
}
