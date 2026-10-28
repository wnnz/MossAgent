using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class TaskSkillOverrideMigrationTests
{
    private static InstalledPlugin Plugin(Guid id) =>
        new(id, "demo", "1.0", new string('a', 64), "demo-1-0-aaaaaaaa",
            InstalledPluginState.Installed, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [new PluginComponentDescriptor(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")],
            [], PluginOverlaySource.None, 1);

    [Fact]
    public async Task FreshDatabaseCreatesTaskSkillOverridesAtVersionFiftyOne()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_overrides';"));
        var orphaned = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO task_skill_overrides VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}','skills/a/SKILL.md',0);"));
        Assert.Equal(19, orphaned.SqliteErrorCode);
        var outOfRange = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO task_skill_overrides VALUES('{fixture.Task.Id}','{Guid.NewGuid()}','skills/a/SKILL.md',2);"));
        Assert.Equal(19, outOfRange.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyOneUpgradeKeepsVersionFiftyDataAndAddsTheTable()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var plugins = new SqliteInstalledPluginStore(fixture.Connections);
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var plugin = await plugins.InsertAsync(Plugin(Guid.NewGuid()),
            new Dictionary<string, string>(), fixture.Token);
        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", false, fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE task_skill_overrides;
            DELETE FROM schema_versions WHERE version>=51;
            """);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_overrides';"));
        Assert.False(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFiftyOneMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE task_skill_overrides;
            DELETE FROM schema_versions WHERE version=51;
            CREATE TRIGGER reject_version_fifty_one BEFORE INSERT ON schema_versions
            WHEN NEW.version=51 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_overrides';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_fifty_one;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=51;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='task_skill_overrides';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyOneReplayAfterVersionRowLossKeepsOverrideRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var overrides = new SqliteTaskSkillOverrides(fixture.Connections);
        var pluginId = Guid.NewGuid();
        await overrides.SetSkillEnabledAsync(fixture.Task.Id, pluginId, "skills/a/SKILL.md",
            false, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=51;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal([new TaskSkillOverride(fixture.Task.Id, pluginId, "skills/a/SKILL.md", false)],
            await overrides.ListForTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
