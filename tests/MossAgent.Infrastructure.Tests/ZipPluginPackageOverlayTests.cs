using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ZipPluginPackageOverlayTests
{
    [Fact]
    public async Task ObjectExtensionsOverlayReplacesAndIgnoresTheCompatibilityFile()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginTestEntry.Text("plugin.json", """{"name":"x","version":"1","extensions":{"com.openai":{"a":1}}}"""),
            PluginTestEntry.Text(".codex-plugin/plugin.json", "{deliberately malformed"));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal(PluginOverlaySource.ExtensionsObject, preview.OverlaySource);
    }

    [Theory]
    [InlineData("""{"name":"x","version":"1","extensions":{"com.openai":"string"}}""")]
    [InlineData("""{"name":"x","version":"1","extensions":{"other":{}}}""")]
    [InlineData("""{"name":"x","version":"1","extensions":"string"}""")]
    [InlineData("""{"name":"x","version":"1"}""")]
    public async Task NonObjectExtensionsFallsBackToAValidCompatibilityFile(string manifest)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginTestEntry.Text("plugin.json", manifest),
            PluginTestEntry.Text(".codex-plugin/plugin.json", """{"legacy":true}"""));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal(PluginOverlaySource.CompatibilityFile, preview.OverlaySource);
    }

    [Theory]
    [InlineData("""{"name":"x","version":"1"}""")]
    [InlineData("""{"name":"x","version":"1","extensions":{"com.openai":"string"}}""")]
    public async Task NoOverlaySourceWhenNothingQualifies(string manifest)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginTestEntry.Text("plugin.json", manifest));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal(PluginOverlaySource.None, preview.OverlaySource);
    }

    [Fact]
    public async Task CompatibilityDirectoryEntryIsNotAnOverlayFile()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginPackageFixture.Manifest(),
            PluginTestEntry.Directory(".codex-plugin/plugin.json"));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal(PluginOverlaySource.None, preview.OverlaySource);
    }

    [Theory]
    [InlineData("{malformed")]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("[1]")]
    public async Task MalformedCompatibilityFileIsRejectedWhenSelected(string compatibility)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginPackageFixture.Manifest(),
            PluginTestEntry.Text(".codex-plugin/plugin.json", compatibility));
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.ManifestInvalid, error.Code);
    }

    [Fact]
    public async Task OversizedCompatibilityFileIsRejectedWhenSelected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginPackageFixture.Manifest(),
            PluginTestEntry.Text(".codex-plugin/plugin.json",
                $$"""{"pad":"{{new string('x', 70 * 1024)}}"}"""));
        var error = await Assert.ThrowsAsync<PluginPackageException>(() =>
            new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token));
        Assert.Equal(PluginPackageErrorCode.ManifestInvalid, error.Code);
    }
}
