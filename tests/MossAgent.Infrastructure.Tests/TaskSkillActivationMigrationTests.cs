using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class TaskSkillActivationMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesTaskSkillActivationsAtVersionFortySeven()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_activations';"));
        var hash = new string('a', 64);
        var orphaned = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO task_skill_activations VALUES('a','{Guid.NewGuid()}','p','demo','1.0','{hash}','skills/a/SKILL.md','body','t');"));
        Assert.Equal(19, orphaned.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO task_skill_activations VALUES('b','{fixture.Task.Id}','p','demo','1.0','{hash}','skills/a/SKILL.md','{new string('x', 8193)}','t');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortySevenMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE task_skill_activations;
            DELETE FROM schema_versions WHERE version=47;
            CREATE TRIGGER reject_version_forty_seven BEFORE INSERT ON schema_versions
            WHEN NEW.version=47 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(47, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_activations';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_seven;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=47;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_activations';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortySevenReplayAfterVersionRowLossKeepsActivationRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteTaskSkillActivationStore(fixture.Connections);
        var row = new TaskSkillActivation(Guid.NewGuid(), fixture.Task.Id, Guid.NewGuid(), "demo",
            "1.0", new string('a', 64), "skills/a/SKILL.md", "pinned body", DateTimeOffset.UnixEpoch);
        await store.SaveForTaskAsync(fixture.Task.Id, [row], fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=47;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal([row], await store.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
