using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Install-time safety: the installer re-runs archive validation itself, so a
/// package swapped or crafted after a clean preview is still rejected before any write.</summary>
public sealed class PluginPackageInstallerSafetyTests
{
    private const string Manifest = """{"name":"demo-plugin","version":"1.2.3"}""";

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("C:/abs/evil.txt")]
    [InlineData("a\\b.txt")]
    public async Task ArchiveSwappedAfterAPassingPreviewIsStillRejected(string entryName)
    {
        await AssertSwappedArchiveRejectedAsync(PluginTestEntry.Text(entryName, "x"));
    }

    [Fact]
    public async Task LinkEntrySwappedAfterAPassingPreviewIsStillRejected()
    {
        await AssertSwappedArchiveRejectedAsync(new PluginTestEntry("linked.txt", "x"u8.ToArray(), 0xA000 << 16));
    }

    private static async Task AssertSwappedArchiveRejectedAsync(PluginTestEntry malicious)
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("safe.txt", "ok"));
        await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        fixture.Packages.WritePackage(PluginTestEntry.Text("plugin.json", Manifest), malicious);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Installer().InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.EntryInvalid, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task CannedPreviewCannotBypassExtractionTimeRevalidation()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("../evil.txt", "x"));
        var canned = new PluginPackagePreview("demo-plugin", "1.2.3", null, path,
            new string('a', 64), 2, 100, [], PluginOverlaySource.None, []);
        var installer = fixture.Installer(previewer: new StubPluginPackagePreviewer(canned));

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            installer.InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.EntryInvalid, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task PackageReplacedBetweenPreviewAndExtractionIsRejected()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        var canned = new PluginPackagePreview("demo-plugin", "1.2.3", null, path,
            new string('a', 64), 2, 100, [], PluginOverlaySource.None, []);
        var installer = fixture.Installer(previewer: new StubPluginPackagePreviewer(canned));

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            installer.InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.False(Directory.Exists(Path.Combine(fixture.PluginRoot, "demo-plugin-1-2-3-aaaaaaaa")));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    public async Task DeclaredLengthLiesAreCaughtByTheIntegrityCheck(uint declared)
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("data.bin", new string('x', 100)));
        PluginPackageLengthPatch.SetDeclaredLength(path, "data.bin", declared);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Installer().InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task DeclaredLimitSizedEntryTruncatedAtCopyIsStillRejected()
    {
        // .NET truncates decompression at the declared size, so a lie sized exactly at the
        // file limit can only be caught by the CRC check on the written bytes.
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest),
            PluginTestEntry.Bytes("blob.bin", 4 * 1024 * 1024 + 4096));
        PluginPackageLengthPatch.SetDeclaredLength(path, "blob.bin", 4 * 1024 * 1024);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Installer().InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task RowWriteFailureRemovesTheMovedPayload()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        var store = new FailingInstalledPluginStore(new InvalidOperationException("synthetic write failure"));
        var installer = fixture.Installer(store: store);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            installer.InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallFailed, error.Code);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        var survivors = Directory.Exists(fixture.PluginRoot)
            ? Directory.EnumerateDirectories(fixture.PluginRoot)
                .Where(directory => Path.GetFileName(directory) != PluginPackageInstaller.StagingDirectoryName)
                .ToArray()
            : [];
        Assert.Empty(survivors);
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task CancellationDuringExtractionCleansStagingWithoutARow()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        using var source = new CancellationTokenSource();
        var installer = fixture.Installer(store: new CancelingInstalledPluginStore(source));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            installer.InstallAsync(path, source.Token));
        Assert.Empty(fixture.StagingEntries());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }

    [Fact]
    public async Task ExistingFinalDirectoryWithoutARowConflicts()
    {
        await using var fixture = await PluginInstallFixture.CreateAsync();
        var path = fixture.Packages.WritePackage(
            PluginTestEntry.Text("plugin.json", Manifest), PluginTestEntry.Text("a.txt", "x"));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        var final = Path.Combine(fixture.PluginRoot, $"demo-plugin-1-2-3-{preview.ContentHash[..8]}");
        Directory.CreateDirectory(final);

        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            fixture.Installer().InstallAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.InstallConflict, error.Code);
        Assert.Empty(fixture.StagingEntries());
        Assert.True(Directory.Exists(final));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM plugin_packages;"));
    }
}
