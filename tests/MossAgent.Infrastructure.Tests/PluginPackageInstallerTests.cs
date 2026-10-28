using System.Security.Cryptography;
using System.Text;
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
    public async Task SameNameWithADifferentVersionUpgradeReplacesTheInstalledRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        var first = await fixture.Installer().InstallAsync(path, fixture.Token);
        var firstPayload = Path.Combine(fixture.PluginRoot, first.InstallPath);
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("a.txt", "second"));
        var second = await fixture.Installer().InstallAsync(path, fixture.Token);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.InstallPath, second.InstallPath);
        Assert.Equal(InstalledPluginState.Installed, second.State);
        Assert.Null(await fixture.Store().GetAsync(first.Id, fixture.Token));
        PluginRowAssert.Equal(second, Assert.Single(await fixture.Installer().ListAsync(fixture.Token)));
        Assert.False(Directory.Exists(firstPayload));
        Assert.True(Directory.Exists(Path.Combine(fixture.PluginRoot, second.InstallPath)));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task SameNameUpgradeResetsAnEnabledRowBackToInstalled()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        var installer = fixture.Installer();
        var first = await installer.InstallAsync(path, fixture.Token);
        Assert.Equal(InstalledPluginState.Enabled,
            (await installer.EnableAsync(first.Id, fixture.Token))!.State);
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("a.txt", "second"));

        var second = await installer.InstallAsync(path, fixture.Token);

        // The replacement carries different bytes, so trust never carries over: the new row
        // waits for an explicit enable and nothing from the old version can still dispatch.
        Assert.Equal(InstalledPluginState.Installed, second.State);
        var row = Assert.Single(await installer.ListAsync(fixture.Token));
        Assert.Equal(second.Id, row.Id);
        Assert.Equal(InstalledPluginState.Installed, row.State);
    }

    [Fact]
    public async Task SameNameUpgradeDeletesEveryOlderRowNotJustTheLatest()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        var first = await fixture.Installer().InstallAsync(path, fixture.Token);
        var seeded = new InstalledPlugin(Guid.NewGuid(), "demo-plugin", "9.9.9", new string('b', 64),
            "demo-plugin-9-9-9-bbbbbbbb", InstalledPluginState.Enabled, "/tmp/seeded.mossplugin",
            DateTimeOffset.UnixEpoch, [], [], PluginOverlaySource.None, 1);
        await fixture.Store().InsertAsync(seeded, new Dictionary<string, string>(), fixture.Token);
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("a.txt", "second"));

        var second = await fixture.Installer().InstallAsync(path, fixture.Token);

        var rows = await fixture.Installer().ListAsync(fixture.Token);
        Assert.Equal(second.Id, Assert.Single(rows).Id);
        Assert.Null(await fixture.Store().GetAsync(first.Id, fixture.Token));
        Assert.Null(await fixture.Store().GetAsync(seeded.Id, fixture.Token));
    }

    [Fact]
    public async Task SameNameUpgradeCascadesTheReplacedRowsSettingsAndHashes()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest),
            PluginTestEntry.Text("skills/a/SKILL.md", "skill a"));
        var first = await fixture.Installer().InstallAsync(path, fixture.Token);
        await new SqlitePluginComponentSettings(fixture.Connections)
            .SetSkillEnabledAsync(first.Id, "skills/a/SKILL.md", false, fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_settings;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("skills/a/SKILL.md", "skill a v2"));

        var second = await fixture.Installer().InstallAsync(path, fixture.Token);

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_settings;"));
        Assert.Empty(await fixture.Store().ListPayloadHashesAsync(first.Id, fixture.Token));
        var baseline = await fixture.Store().ListPayloadHashesAsync(second.Id, fixture.Token);
        Assert.Equal(Sha256("skill a v2"), baseline["skills/a/SKILL.md"]);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task FailedReplaceKeepsTheOldRowInstalledAndCleansTheNewPayload()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "first"));
        var first = await fixture.Installer().InstallAsync(path, fixture.Token);
        var failing = new FailingReplaceInstalledPluginStore(fixture.Store(),
            new InvalidOperationException("synthetic replace failure"));
        var installer = fixture.Installer(store: failing);
        fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"demo-plugin","version":"1.2.4"}"""),
            PluginTestEntry.Text("a.txt", "second"));

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            installer.InstallAsync(path, fixture.Token));

        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(1, failing.ReplaceCalls);
        PluginRowAssert.Equal(first, await fixture.Store().GetAsync(first.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.True(Directory.Exists(Path.Combine(fixture.PluginRoot, first.InstallPath)));
        var survivors = Directory.EnumerateDirectories(fixture.PluginRoot)
            .Select(directory => Path.GetFileName(directory)!)
            .Where(name => name != PluginPackageInstaller.StagingDirectoryName)
            .ToArray();
        Assert.Equal([first.InstallPath], survivors);
        Assert.Empty(fixture.StagingEntries());
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

    [Fact]
    public async Task InstallPersistsExactlyTheComponentPayloadHashes()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());

        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);

        var hashes = await fixture.Store().ListPayloadHashesAsync(installed.Id, fixture.Token);
        Assert.Equal(
            ["assets/logo.png", "docs/intro.md", "mcp.json", "readme.txt", "skills/a/SKILL.md"],
            hashes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Sha256("skill a"), hashes["skills/a/SKILL.md"]);
        Assert.Equal(Sha256("""{"mcpServers":{"beta":{"type":"http"},"alpha":{"type":"stdio"}}}"""),
            hashes["mcp.json"]);
        Assert.Equal(Sha256("readme"), hashes["readme.txt"]);
        Assert.Equal(Sha256("intro"), hashes["docs/intro.md"]);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(new byte[32])), hashes["assets/logo.png"]);
        Assert.Equal(5, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ManifestComponentWithoutAnExtractedFileFailsTheInstall()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        var ghosted = preview with
        {
            Components = [.. preview.Components,
                new PluginComponentDescriptor(PluginComponentKind.Skill, "ghost", "skills/ghost/SKILL.md")]
        };
        var installer = fixture.Installer(previewer: new StubPluginPackagePreviewer(ghosted));

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            installer.InstallAsync(path, fixture.Token));

        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ReconcileBackfillsMissingBaselineRowsFromTheOnDiskPayload()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM plugin_component_hashes;");

        await fixture.Installer().ReconcileAsync(fixture.Token);

        var hashes = await fixture.Store().ListPayloadHashesAsync(installed.Id, fixture.Token);
        Assert.Equal(
            ["assets/logo.png", "docs/intro.md", "mcp.json", "readme.txt", "skills/a/SKILL.md"],
            hashes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Sha256("skill a"), hashes["skills/a/SKILL.md"]);
        Assert.Equal(Sha256("readme"), hashes["readme.txt"]);
        Assert.Equal(5, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    [Fact]
    public async Task ReconcileLeavesDeletedComponentFilesWithoutABaselineRow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(FullPackage());
        var installed = await fixture.Installer().InstallAsync(path, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM plugin_component_hashes;");
        File.Delete(Path.Combine(fixture.PluginRoot, installed.InstallPath, "readme.txt"));

        await fixture.Installer().ReconcileAsync(fixture.Token);

        var hashes = await fixture.Store().ListPayloadHashesAsync(installed.Id, fixture.Token);
        Assert.Equal(
            ["assets/logo.png", "docs/intro.md", "mcp.json", "skills/a/SKILL.md"],
            hashes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(4, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_component_hashes;"));
    }

    private static string Sha256(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
