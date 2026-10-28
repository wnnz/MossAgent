using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginPackageMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesPluginPackagesAtVersionFortySix()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_packages';"));
        var hash = new string('a', 64);
        var duplicate = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_packages VALUES('a','demo','1.0','{hash}','p',0,'s','t','[]','[]',0);"
            + $"INSERT INTO plugin_packages VALUES('b','demo','1.0','{hash}','p2',0,'s','t','[]','[]',0);"));
        Assert.Equal(19, duplicate.SqliteErrorCode);
        var badState = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO plugin_packages VALUES('c','demo','2.0','{hash}','p',7,'s','t','[]','[]',0);"));
        Assert.Equal(19, badState.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortySixMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE plugin_packages;
            DELETE FROM schema_versions WHERE version=46;
            CREATE TRIGGER reject_version_forty_six BEFORE INSERT ON schema_versions
            WHEN NEW.version=46 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(50, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_packages';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_six;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=46;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='plugin_packages';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortySixReplayAfterVersionRowLossKeepsInstalledRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteInstalledPluginStore(fixture.Connections);
        var plugin = new InstalledPlugin(Guid.NewGuid(), "demo", "1.0", new string('a', 64),
            "demo-1-0-aaaaaaaa", InstalledPluginState.Installed, "/tmp/p.mossplugin",
            DateTimeOffset.UnixEpoch, [], [], PluginOverlaySource.None);
        await store.InsertAsync(plugin, new Dictionary<string, string>(), fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=46;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        PluginRowAssert.Equal(plugin, await store.GetAsync(plugin.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
