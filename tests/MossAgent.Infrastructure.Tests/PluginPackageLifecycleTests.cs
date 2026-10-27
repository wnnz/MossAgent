using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Lifecycle transitions on installed rows: enable/disable are persisted state
/// flags only (no component becomes executable), and uninstall deletes the row first so a
/// payload that survives is reported as an orphan rather than leaving a dangling row.</summary>
public sealed class PluginPackageLifecycleTests
{
    private const string Manifest = """{"name":"demo-plugin","version":"1.2.3"}""";

    [Fact]
    public async Task EnableAndDisableUpdateThePersistedState()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(PluginTestEntry.Text("plugin.json", Manifest));
        var installer = fixture.Installer();
        var installed = await installer.InstallAsync(path, fixture.Token);
        Assert.Equal(InstalledPluginState.Installed, installed.State);
        Assert.Equal(InstalledPluginState.Enabled,
            (await installer.EnableAsync(installed.Id, fixture.Token))!.State);
        PluginRowAssert.Equal(installed with { State = InstalledPluginState.Enabled },
            await fixture.Store().GetAsync(installed.Id, fixture.Token));
        Assert.Equal(InstalledPluginState.Disabled,
            (await installer.DisableAsync(installed.Id, fixture.Token))!.State);
        Assert.Equal(InstalledPluginState.Disabled,
            (await fixture.Store().GetAsync(installed.Id, fixture.Token))!.State);
        Assert.Null(await installer.EnableAsync(Guid.NewGuid(), fixture.Token));
        Assert.Null(await installer.DisableAsync(Guid.NewGuid(), fixture.Token));
    }

    [Fact]
    public async Task UninstallRemovesTheRowAndPayloadButNeverTheSourcePackage()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        var installer = fixture.Installer();
        var installed = await installer.InstallAsync(path, fixture.Token);
        var payload = Path.Combine(fixture.PluginRoot, installed.InstallPath);
        Assert.True(Directory.Exists(payload));

        var outcome = await installer.UninstallAsync(installed.Id, fixture.Token);

        PluginRowAssert.Equal(installed, outcome?.Plugin);
        Assert.True(outcome!.PayloadRemoved);
        Assert.False(Directory.Exists(payload));
        Assert.True(File.Exists(path));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Null(await installer.UninstallAsync(installed.Id, fixture.Token));
    }

    [Fact]
    public async Task UninstallThatCannotRemoveThePayloadLeavesAnOrphanForReconciliation()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        var installer = fixture.Installer();
        var installed = await installer.InstallAsync(path, fixture.Token);
        var payload = Path.Combine(fixture.PluginRoot, installed.InstallPath);
        await using var held = new FileStream(Path.Combine(payload, "a.txt"), FileMode.Open,
            FileAccess.Read, FileShare.Read);

        var outcome = await installer.UninstallAsync(installed.Id, fixture.Token);

        PluginRowAssert.Equal(installed, outcome?.Plugin);
        Assert.False(outcome!.PayloadRemoved);
        Assert.True(Directory.Exists(payload));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        var reconciliation = await installer.ReconcileAsync(fixture.Token);
        Assert.Equal([installed.InstallPath], reconciliation.OrphanedFinals);
        Assert.True(Directory.Exists(payload));
    }
}
