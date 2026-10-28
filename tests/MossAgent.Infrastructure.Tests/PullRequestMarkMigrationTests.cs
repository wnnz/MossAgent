using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestMarkMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesMarksAtVersionFortyFour()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='pull_request_marks';"));
        var head = new string('b', 40);
        var account = Guid.NewGuid().ToString("D");
        var zeroNumber = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','repo',0,'{head}',1);"));
        Assert.Equal(19, zeroNumber.SqliteErrorCode);
        var shortHead = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','repo',7,'{head[..39]}',1);"));
        Assert.Equal(19, shortHead.SqliteErrorCode);
        var longHead = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','repo',7,'{head + new string('b', 25)}',1);"));
        Assert.Equal(19, longHead.SqliteErrorCode);
        var emptyOwner = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','','repo',7,'{head}',1);"));
        Assert.Equal(19, emptyOwner.SqliteErrorCode);
        var longOwner = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','{new string('o', 101)}','repo',7,'{head}',1);"));
        Assert.Equal(19, longOwner.SqliteErrorCode);
        var longRepository = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','{new string('r', 101)}',7,'{head}',1);"));
        Assert.Equal(19, longRepository.SqliteErrorCode);
        // Marks are per-account bookkeeping: no FK to source_accounts and no account row is needed.
        await fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','repo',7,'{head}',1);");
        var duplicate = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO pull_request_marks VALUES('{account}','fixture','repo',7,'{head}',2);"));
        Assert.Equal(19, duplicate.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyFourMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE pull_request_marks;
            DELETE FROM schema_versions WHERE version=44;
            CREATE TRIGGER reject_version_forty_four BEFORE INSERT ON schema_versions
            WHEN NEW.version=44 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(50, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='pull_request_marks';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_four;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=44;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='pull_request_marks';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyFourUpgradePreservesWorkspaceRowsAndMarksRoundTrip()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        var account = Guid.NewGuid();
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, new string('b', 40), fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE pull_request_marks;
            DELETE FROM schema_versions WHERE version=44;
            """);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Empty(await marks.GetAsync(account, "fixture", "repo", [7], fixture.Token));
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, new string('c', 40), fixture.Token);
        var stored = Assert.Single(await marks.GetAsync(account, "fixture", "repo", [7], fixture.Token));
        Assert.Equal(new string('c', 40), stored.HeadRevision);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
