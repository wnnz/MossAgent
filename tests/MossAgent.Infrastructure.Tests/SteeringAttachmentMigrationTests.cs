using Microsoft.Data.Sqlite;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SteeringAttachmentMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesSteeringAttachmentsAtVersionThirtyNine()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_steering_input_attachments';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_steering_input_attachments_task';"));
        var invalidHash = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("""
            INSERT INTO conversation_steering_input_attachments(input_id,task_id,ordinal,display_path,byte_count,sha256,media_type,attachment_id)
            VALUES('x','t',0,'a.txt',4,'not-a-hash','text/plain','att');
            """));
        Assert.Equal(19, invalidHash.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO conversation_steering_input_attachments(input_id,task_id,ordinal,display_path,byte_count,sha256,media_type,attachment_id) "
            + "VALUES('x','t',0,'a.txt',200000,'" + new string('a', 64) + "','text/plain','att');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
    }

    [Fact]
    public async Task FailedThirtyNineMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_steering_input_attachments;
            DELETE FROM schema_versions WHERE version=39;
            CREATE TRIGGER reject_version_thirty_nine BEFORE INSERT ON schema_versions
            WHEN NEW.version=39 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        // v39 was removed and its migration rejected, but v40 remains applied.
        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_steering_input_attachments';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirty_nine;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=39;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_steering_input_attachments';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ThirtyNineMigrationPreservesExistingSteeringRows()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var input = await fixture.AcceptAsync(run, "pre-migration guidance");
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_steering_input_attachments;
            DELETE FROM schema_versions WHERE version=39;
            """);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var restored = Assert.Single((await fixture.InputsAsync()).Items);
        Assert.Equal(input.Id, restored.Id);
        Assert.Equal(input.Text, restored.Text);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
