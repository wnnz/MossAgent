using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginPackageInstallerTests
{
    private const string Manifest = """{"name":"demo-plugin","version":"1.2.3","description":"demo"}""";

    private static PluginTestEntry[] FullPackage() =>
    [
        PluginTestEntry.Text("plugin.json", Manifest),
        PluginTestEntry.Text("skills/a/SKILL.md", "skill a"),
        PluginTestEntry.Text("mcp.json", """{"mcpServers":{"beta":{"type":"http"},"alpha":{"type":"stdio"}}}"""),
        PluginTestEntry.Text("readme.txt", "readme"),
        PluginTestEntry.Bytes("assets/logo.png", 32),
        PluginTestEntry.Directory("docs"),
        PluginTestEntry.Text("docs/intro.md", "intro")
    ];

    [Fact]
    public async Task InstallExtractsPayloadByteForByteAndPersistsTheRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        fixture.Time.Advance(TimeSpan.FromSeconds(90));
        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);

        Assert.Equal("demo-plugin", installed.Name);
        Assert.Equal("1.2.3", installed.Version);
        Assert.Equal(preview.ContentHash, installed.ContentHash);
        Assert.Equal(InstalledPluginState.Installed, installed.State);
        Assert.Equal(preview.SourcePath, installed.SourcePath);
        Assert.Equal(fixture.Time.GetUtcNow(), installed.InstalledAt);
        Assert.Equal(preview.Components, installed.Components);
        Assert.Equal(preview.DeclaredMcpServers, installed.DeclaredMcpServers);
        Assert.Equal(preview.OverlaySource, installed.OverlaySource);
        var directory = $"demo-plugin-1-2-3-{preview.ContentHash[..8]}";
        Assert.Equal(directory, installed.InstallPath);

        var final = Path.Combine(fixture.PluginRoot, directory);
        Assert.Equal("skill a", File.ReadAllText(Path.Combine(final, "skills/a/SKILL.md")));
        Assert.Equal("readme", File.ReadAllText(Path.Combine(final, "readme.txt")));
        Assert.Equal("intro", File.ReadAllText(Path.Combine(final, "docs/intro.md")));
        Assert.Equal(new byte[32], File.ReadAllBytes(Path.Combine(final, "assets/logo.png")));
        Assert.True(Directory.Exists(Path.Combine(final, "docs")));
        Assert.Empty(fixture.StagingEntries());

        PluginRowAssert.Equal(installed, await fixture.Store().GetAsync(installed.Id, fixture.Token));
        PluginRowAssert.Equal(installed, await fixture.Installer().FindAsync(installed.Id, fixture.Token));
        PluginRowAssert.Equal(installed, Assert.Single(await fixture.Installer().ListAsync(fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ReinstallOfIdenticalContentReturnsTheSameRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);
        var again = await fixture.Installer().InstallAsync(path, fixture.Token);
        Assert.Equal(installed.Id, again.Id);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task SameNameAndVersionWithDifferentContentConflicts()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        await fixture.Installer().InstallAsync(path, fixture.Token);
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "changed"));
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Installer().InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallConflict, error.Code);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task SameNameWithADifferentVersionInstallsSeparately()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        var first = await fixture.Installer().InstallAsync(path, fixture.Token);
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("a.txt", "second"));
        var second = await fixture.Installer().InstallAsync(path, fixture.Token);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.InstallPath, second.InstallPath);
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Equal(2, (await fixture.Installer().ListAsync(fixture.Token)).Count);
    }

    [Fact]
    public async Task ReconcileRemovesStagingAndOnlyReportsOrphanedFinals()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);
        var leftover = Path.Combine(fixture.StagingRoot, "deadbeef");
        Directory.CreateDirectory(Path.Combine(leftover, "inner"));
        var orphan = Path.Combine(fixture.PluginRoot, "orphan-final");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "stray.txt"), "x");

        var result = await fixture.Installer().ReconcileAsync(fixture.Token);

        Assert.Equal(1, result.RemovedStaging);
        Assert.Equal(["orphan-final"], result.OrphanedFinals);
        Assert.False(Directory.Exists(leftover));
        Assert.True(Directory.Exists(orphan));
        Assert.True(Directory.Exists(Path.Combine(fixture.PluginRoot, installed.InstallPath)));
        Assert.Empty(fixture.StagingEntries());
    }
}
