using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginTrustRevisionMigrationTests
{
    private const string PluginColumn =
        "SELECT COUNT(*) FROM pragma_table_info('plugin_packages') WHERE name='trust_revision';";
    private const string ActivationColumn =
        "SELECT COUNT(*) FROM pragma_table_info('task_skill_activations') WHERE name='trust_revision';";

    private static InstalledPlugin Plugin(Guid id) =>
        new(id, "demo", "1.0", new string('a', 64), "demo-1-0-aaaaaaaa",
            InstalledPluginState.Installed, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [new PluginComponentDescriptor(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")],
            [], PluginOverlaySource.None, 1);

    [Fact]
    public async Task FreshDatabaseAddsTrustRevisionColumnsAtVersionFiftyTwo()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(PluginColumn));
        Assert.Equal(1, await fixture.CountAsync(ActivationColumn));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyTwoUpgradeGivesExistingRowsRevisionOne()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var hash = new string('a', 64);
        var pluginId = Guid.NewGuid();
        var activationId = Guid.NewGuid();
        // Simulate the pre-v52 upgrade path: tables without the column carry rows that gain
        // the honest baseline of 1 when the column is added.
        await fixture.ExecuteAsync("""
            ALTER TABLE plugin_packages DROP COLUMN trust_revision;
            ALTER TABLE task_skill_activations DROP COLUMN trust_revision;
            DELETE FROM schema_versions WHERE version=52;
            """);
        var at = DateTimeOffset.UnixEpoch.ToString("O");
        await fixture.ExecuteAsync(
            $"INSERT INTO plugin_packages VALUES('{pluginId}','demo','1.0','{hash}','p',0,'s','{at}','[]','[]',0);");
        await fixture.ExecuteAsync(
            $"INSERT INTO task_skill_activations VALUES('{activationId}','{fixture.Task.Id}','{pluginId}',"
            + $"'demo','1.0','{hash}','skills/a/SKILL.md','body','{at}');");

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var plugin = await new SqliteInstalledPluginStore(fixture.Connections)
            .GetAsync(pluginId, fixture.Token);
        Assert.Equal(1, plugin!.TrustRevision);
        var rows = await new SqliteTaskSkillActivationStore(fixture.Connections)
            .ListForTaskAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(1, Assert.Single(rows).TrustRevision);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyTwoReplayAfterVersionRowLossKeepsColumnsAndRevisions()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteInstalledPluginStore(fixture.Connections);
        var plugin = await store.InsertAsync(Plugin(Guid.NewGuid()),
            new Dictionary<string, string>(), fixture.Token);
        var enabled = await store.SetStateAsync(plugin.Id, InstalledPluginState.Enabled, fixture.Token);
        Assert.Equal(2, enabled!.TrustRevision);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=52;");

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(PluginColumn));
        Assert.Equal(1, await fixture.CountAsync(ActivationColumn));
        PluginRowAssert.Equal(enabled, await store.GetAsync(plugin.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FiftyReplayRebuildRestoresTheColumnBehindTheVersionRow()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var store = new SqliteInstalledPluginStore(fixture.Connections);
        var plugin = await store.InsertAsync(Plugin(Guid.NewGuid()),
            new Dictionary<string, string>(), fixture.Token);
        await store.SetStateAsync(plugin.Id, InstalledPluginState.Enabled, fixture.Token);
        // Replaying the v50 rebuild drops trust_revision while the v52 version row survives;
        // the migration repairs the column instead of trusting the version row alone, and the
        // rebuild's lost history honestly restarts at the baseline.
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=50;");

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(PluginColumn));
        var row = await store.GetAsync(plugin.Id, fixture.Token);
        Assert.Equal(InstalledPluginState.Enabled, row!.State);
        Assert.Equal(1, row.TrustRevision);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFiftyTwoMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            ALTER TABLE plugin_packages DROP COLUMN trust_revision;
            DELETE FROM schema_versions WHERE version=52;
            CREATE TRIGGER reject_version_fifty_two BEFORE INSERT ON schema_versions
            WHEN NEW.version=52 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(PluginColumn));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_fifty_two;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=52;"));
        Assert.Equal(1, await fixture.CountAsync(PluginColumn));
        Assert.Equal(1, await fixture.CountAsync(ActivationColumn));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
