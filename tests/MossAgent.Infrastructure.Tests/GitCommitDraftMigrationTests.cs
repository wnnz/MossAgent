using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitCommitDraftMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesCommitDraftsAtVersionForty()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='git_commit_drafts';"));
        var missingTask = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO git_commit_drafts(task_id,text,revision,updated_at) VALUES('missing','text',1,'now');"));
        Assert.Equal(19, missingTask.SqliteErrorCode);
        var emptyText = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO git_commit_drafts(task_id,text,revision,updated_at) VALUES('{fixture.Task.Id}','',1,'now');"));
        Assert.Equal(19, emptyText.SqliteErrorCode);
        var zeroRevision = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO git_commit_drafts(task_id,text,revision,updated_at) VALUES('{fixture.Task.Id}','text',0,'now');"));
        Assert.Equal(19, zeroRevision.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO git_commit_drafts(task_id,text,revision,updated_at) VALUES('{fixture.Task.Id}','"
            + new string('x', 8193) + "',1,'now');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE git_commit_drafts;
            DELETE FROM schema_versions WHERE version=40;
            CREATE TRIGGER reject_version_forty BEFORE INSERT ON schema_versions
            WHEN NEW.version=40 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='git_commit_drafts';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=40;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='git_commit_drafts';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyUpgradePreservesExistingWorkspaceRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User,
            "preserved", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.ExecuteAsync($"""
            INSERT INTO conversation_drafts(scope_key,project_id,task_id,text,revision)
            VALUES('task:{fixture.Task.Id}','{fixture.Project.Id}','{fixture.Task.Id}','older draft',3);
            """);
        await fixture.ExecuteAsync("""
            DROP TABLE git_commit_drafts;
            DELETE FROM schema_versions WHERE version=40;
            """);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(40, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM conversation_drafts WHERE text='older draft' AND revision=3;"));
        var repository = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "new draft", 0), fixture.Token));
        Assert.Equal(new("new draft", 1), await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
