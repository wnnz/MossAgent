using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginComponentSettingsMigrationTests
{
    private static InstalledPlugin Plugin(Guid id) =>
        new(id, "demo", "1.0", new string('a', 64), "demo-1-0-aaaaaaaa",
            InstalledPluginState.Installed, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [], [], PluginOverlaySource.None);

    [Fact]
    public async Task FreshDatabaseCreatesPluginComponentSettingsAtVersionFortyNine()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(49, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_settings';"));
        var plugin = await new SqliteInstalledPluginStore(fixture.Connections)
            .InsertAsync(Plugin(Guid.NewGuid()), fixture.Token);
        var orphan = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_settings VALUES('{Guid.NewGuid()}','skills/a/SKILL.md',0,'t');"));
        Assert.Equal(19, orphan.SqliteErrorCode);
        var outOfRange = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_settings VALUES('{plugin.Id}','skills/a/SKILL.md',2,'t');"));
        Assert.Equal(19, outOfRange.SqliteErrorCode);
        var emptyPath = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_settings VALUES('{plugin.Id}','',0,'t');"));
        Assert.Equal(19, emptyPath.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyNineMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE plugin_component_settings;
            DELETE FROM schema_versions WHERE version=49;
            CREATE TRIGGER reject_version_forty_nine BEFORE INSERT ON schema_versions
            WHEN NEW.version=49 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(48, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_settings';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_nine;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(49, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=49;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_settings';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyNineReplayAfterVersionRowLossKeepsChoices()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var plugins = new SqliteInstalledPluginStore(fixture.Connections);
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var plugin = await plugins.InsertAsync(Plugin(Guid.NewGuid()), fixture.Token);
        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", false, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=49;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(49, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.False(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
