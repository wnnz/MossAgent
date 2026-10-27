using Microsoft.Data.Sqlite;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqlitePluginComponentSettingsTests
{
    private static InstalledPlugin Plugin(string name = "demo") =>
        new(Guid.NewGuid(), name, "1.0", new string('a', 64), $"{name}-1-0-aaaaaaaa",
            InstalledPluginState.Installed, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [new PluginComponentDescriptor(PluginComponentKind.Skill, "a", "skills/a/SKILL.md")],
            [], PluginOverlaySource.None);

    [Fact]
    public async Task AbsentRowsReportTheDefaultEnabledStateAndNoOverrides()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var plugin = await fixture.Store().InsertAsync(Plugin(), new Dictionary<string, string>(), fixture.Token);

        Assert.True(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Empty(await settings.ListOverridesAsync(plugin.Id, fixture.Token));
    }

    [Fact]
    public async Task ExplicitChoicesRoundTripAndExplicitReEnableStaysStored()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var plugin = await fixture.Store().InsertAsync(Plugin(), new Dictionary<string, string>(), fixture.Token);

        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", false, fixture.Token);
        Assert.False(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Equal(new Dictionary<string, bool> { ["skills/a/SKILL.md"] = false },
            await settings.ListOverridesAsync(plugin.Id, fixture.Token));

        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", true, fixture.Token);
        Assert.True(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Equal(new Dictionary<string, bool> { ["skills/a/SKILL.md"] = true },
            await settings.ListOverridesAsync(plugin.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_component_settings;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task OverridesAreScopedPerPluginRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var alpha = await fixture.Store().InsertAsync(Plugin("alpha"), new Dictionary<string, string>(), fixture.Token);
        var beta = await fixture.Store().InsertAsync(Plugin("beta"), new Dictionary<string, string>(), fixture.Token);

        await settings.SetSkillEnabledAsync(alpha.Id, "skills/a/SKILL.md", false, fixture.Token);

        Assert.False(await settings.IsSkillEnabledAsync(alpha.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.True(await settings.IsSkillEnabledAsync(beta.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Empty(await settings.ListOverridesAsync(beta.Id, fixture.Token));
        Assert.Single(await settings.ListOverridesAsync(alpha.Id, fixture.Token));
    }

    [Fact]
    public async Task DeletingThePluginRowCascadesItsChoices()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var settings = new SqlitePluginComponentSettings(fixture.Connections);
        var store = fixture.Store();
        var plugin = await store.InsertAsync(Plugin(), new Dictionary<string, string>(), fixture.Token);
        await settings.SetSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", false, fixture.Token);

        await store.DeleteAsync(plugin.Id, fixture.Token);

        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_component_settings;"));
        Assert.True(await settings.IsSkillEnabledAsync(plugin.Id, "skills/a/SKILL.md", fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task WritingAgainstAMissingPluginFailsOnTheForeignKey()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var settings = new SqlitePluginComponentSettings(fixture.Connections);

        var error = await Assert.ThrowsAsync<SqliteException>(() =>
            settings.SetSkillEnabledAsync(Guid.NewGuid(), "skills/a/SKILL.md", false, fixture.Token));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM plugin_component_settings;"));
    }
}
