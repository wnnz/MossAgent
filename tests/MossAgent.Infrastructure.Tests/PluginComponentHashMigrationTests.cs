using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginComponentHashMigrationTests
{
    private static InstalledPlugin Plugin(Guid id) =>
        new(id, "demo", "1.0", new string('a', 64), "demo-1-0-aaaaaaaa",
            InstalledPluginState.Installed, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [new PluginComponentDescriptor(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")],
            [], PluginOverlaySource.None);

    [Fact]
    public async Task FreshDatabaseCreatesPluginComponentHashesAtVersionFifty()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(50, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_hashes';"));
        var plugin = await new SqliteInstalledPluginStore(fixture.Connections)
            .InsertAsync(Plugin(Guid.NewGuid()), new Dictionary<string, string>
            {
                ["skills/a/SKILL.md"] = new string('a', 64)
            }, fixture.Token);
        var orphan = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_hashes VALUES('{Guid.NewGuid()}','skills/a/SKILL.md','{new string('a', 64)}');"));
        Assert.Equal(19, orphan.SqliteErrorCode);
        var emptyPath = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_hashes VALUES('{plugin.Id}','','{new string('a', 64)}');"));
        Assert.Equal(19, emptyPath.SqliteErrorCode);
        var emptyHash = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_hashes VALUES('{plugin.Id}','skills/b/SKILL.md','');"));
        Assert.Equal(19, emptyHash.SqliteErrorCode);
        var oversizedHash = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_hashes VALUES('{plugin.Id}','skills/b/SKILL.md','{new string('a', 129)}');"));
        Assert.Equal(19, oversizedHash.SqliteErrorCode);
        var oversizedPath = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_component_hashes VALUES('{plugin.Id}','{new string('p', 261)}','{new string('a', 64)}');"));
        Assert.Equal(19, oversizedPath.SqliteErrorCode);
        await fixture.ExecuteAsync($"UPDATE plugin_packages SET state=3 WHERE id='{plugin.Id}';");
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_packages WHERE state=3;"));
        var badState = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"UPDATE plugin_packages SET state=7 WHERE id='{plugin.Id}';"));
        Assert.Equal(19, badState.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFiftyMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var plugin = await new SqliteInstalledPluginStore(fixture.Connections)
            .InsertAsync(Plugin(Guid.NewGuid()), new Dictionary<string, string>(), fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE plugin_component_hashes;
            DELETE FROM schema_versions WHERE version=50;
            CREATE TRIGGER reject_version_fifty BEFORE INSERT ON schema_versions
            WHEN NEW.version=50 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(49, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_hashes';"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_packages_v50';"));
        // The rollback restores the database exactly as the failed attempt found it — the
        // plugin row survives untouched and only the replay rewrites the version row.
        Assert.Equal(1, await fixture.CountAsync(
            $"SELECT COUNT(*) FROM plugin_packages WHERE id='{plugin.Id}';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_fifty;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(50, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=50;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_component_hashes';"));
        await fixture.ExecuteAsync($"UPDATE plugin_packages SET state=3 WHERE id='{plugin.Id}';");
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyReplayAfterVersionRowLossKeepsPluginsAndBaselines()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteInstalledPluginStore(fixture.Connections);
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var plugin = await store.InsertAsync(Plugin(Guid.NewGuid()), new Dictionary<string, string>
        {
            ["skills/a/SKILL.md"] = new string('a', 64)
        }, fixture.Token);
        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", false, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=50;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(50, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(new string('a', 64),
            (await store.ListPayloadHashesAsync(plugin.Id, fixture.Token))["skills/a/SKILL.md"]);
        Assert.False(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        PluginRowAssert.Equal(plugin, await store.GetAsync(plugin.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
