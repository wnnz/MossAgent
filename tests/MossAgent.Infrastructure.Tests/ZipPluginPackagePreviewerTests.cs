using System.Security.Cryptography;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ZipPluginPackagePreviewerTests
{
    private const string OverlayManifest = """
        {"name":"demo-plugin","version":"1.2.3","description":"demo package",
         "extensions":{"com.openai":{"theme":"dark"}}}
        """;

    [Fact]
    public async Task ValidPackageProducesDeterministicPreview()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(FullPackageEntries());
        var previewer = new ZipPluginPackagePreviewer();
        var preview = await previewer.PreviewAsync(path, fixture.Token);

        Assert.Equal("demo-plugin", preview.Name);
        Assert.Equal("1.2.3", preview.Version);
        Assert.Equal("demo package", preview.Description);
        Assert.Equal(Path.GetFullPath(path), preview.SourcePath);
        Assert.Equal(10, preview.EntryCount);
        Assert.Equal(PluginOverlaySource.ExtensionsObject, preview.OverlaySource);
        Assert.Equal(
            [new PluginMcpServerDeclaration("beta", "http"), new PluginMcpServerDeclaration("alpha", "stdio")],
            preview.DeclaredMcpServers);
        Assert.Equal(UncompressedBytes(path), preview.TotalUncompressedBytes);
        Assert.Equal(ExpectedComponents, preview.Components);
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path, fixture.Token)));
        Assert.Equal(expectedHash, preview.ContentHash);
        Assert.Equal(expectedHash, (await previewer.PreviewAsync(path, fixture.Token)).ContentHash);
    }

    private static PluginTestEntry[] FullPackageEntries() =>
    [
        PluginTestEntry.Text("plugin.json", OverlayManifest),
        PluginTestEntry.Text(".codex-plugin/plugin.json", "{malformed"),
        PluginTestEntry.Text("skills/b/SKILL.md", "skill b"),
        PluginTestEntry.Text("skills/a/SKILL.md", "skill a"),
        PluginTestEntry.Text("mcp.json", """{"mcpServers":{"beta":{"type":"http"},"alpha":{"type":"stdio"}}}"""),
        PluginTestEntry.Text("readme.txt", "readme"),
        PluginTestEntry.Bytes("assets/logo.png", 32),
        PluginTestEntry.Directory("docs"),
        PluginTestEntry.Text("sub/plugin.json", "{}"),
        PluginTestEntry.Text(".mcp.json", """{"mcpServers":{"legacy":{"type":"stdio"}}}""")
    ];

    private static PluginComponentDescriptor[] ExpectedComponents =>
    [
        new PluginComponentDescriptor(PluginComponentKind.Skill, "a", "skills/a/SKILL.md"),
        new PluginComponentDescriptor(PluginComponentKind.Skill, "b", "skills/b/SKILL.md"),
        new PluginComponentDescriptor(PluginComponentKind.McpServer, "alpha", "mcp.json"),
        new PluginComponentDescriptor(PluginComponentKind.McpServer, "beta", "mcp.json"),
        new PluginComponentDescriptor(PluginComponentKind.Asset, ".mcp.json", ".mcp.json"),
        new PluginComponentDescriptor(PluginComponentKind.Asset, "docs", "docs"),
        new PluginComponentDescriptor(PluginComponentKind.Asset, "logo.png", "assets/logo.png"),
        new PluginComponentDescriptor(PluginComponentKind.Asset, "plugin.json", "sub/plugin.json"),
        new PluginComponentDescriptor(PluginComponentKind.Asset, "readme.txt", "readme.txt")
    ];

    private static long UncompressedBytes(string path)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        return archive.Entries.Where(entry => !entry.FullName.EndsWith('/')).Sum(entry => entry.Length);
    }

    [Fact]
    public async Task ServersKeyVariantIsAcceptedAsPortableDeclaration()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginPackageFixture.Manifest(),
            PluginTestEntry.Text("mcp.json", """{"servers":{"local":{"type":"stdio","command":"run"}}}"""));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal([new PluginMcpServerDeclaration("local", "stdio")], preview.DeclaredMcpServers);
    }

    [Fact]
    public async Task DottedMcpJsonIsNeverParsedAndStaysAPlainAsset()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(
            PluginPackageFixture.Manifest(),
            PluginTestEntry.Text(".mcp.json", "{not even json"));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Empty(preview.DeclaredMcpServers);
        Assert.Equal(new PluginComponentDescriptor(PluginComponentKind.Asset, ".mcp.json", ".mcp.json"),
            Assert.Single(preview.Components));
    }

    [Fact]
    public async Task MissingRootManifestIsRejected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginTestEntry.Text("readme.txt", "readme"),
            PluginTestEntry.Text("sub/plugin.json", """{"name":"x","version":"1"}"""));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.ManifestMissing, error.Code);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("array-root")]
    [InlineData("no-name")]
    [InlineData("empty-name")]
    [InlineData("blank-name")]
    [InlineData("number-name")]
    [InlineData("long-name")]
    [InlineData("no-version")]
    [InlineData("empty-version")]
    [InlineData("number-version")]
    [InlineData("long-version")]
    [InlineData("number-description")]
    public async Task InvalidManifestIsRejectedWithoutDegrading(string scenario)
    {
        using var fixture = new PluginPackageFixture();
        var manifest = scenario switch
        {
            "malformed" => "{",
            "oversized" => $$"""{"name":"x","version":"1","pad":"{{new string('x', 70 * 1024)}}"}""",
            "array-root" => "[]",
            "no-name" => """{"version":"1"}""",
            "empty-name" => """{"name":"","version":"1"}""",
            "blank-name" => """{"name":"   ","version":"1"}""",
            "number-name" => """{"name":7,"version":"1"}""",
            "long-name" => $$"""{"name":"{{new string('x', 201)}}","version":"1"}""",
            "no-version" => """{"name":"x"}""",
            "empty-version" => """{"name":"x","version":""}""",
            "number-version" => """{"name":"x","version":2}""",
            "long-version" => $$"""{"name":"x","version":"{{new string('x', 101)}}"}""",
            _ => """{"name":"x","version":"1","description":[]}"""
        };
        var path = fixture.WritePackage(PluginTestEntry.Text("plugin.json", manifest));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.ManifestInvalid, error.Code);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("oversized")]
    [InlineData("array-root")]
    [InlineData("no-servers")]
    [InlineData("servers-not-object")]
    [InlineData("entry-not-object")]
    [InlineData("missing-type")]
    [InlineData("number-type")]
    [InlineData("empty-type")]
    [InlineData("empty-name")]
    public async Task InvalidMcpManifestIsRejectedWithoutDegrading(string scenario)
    {
        using var fixture = new PluginPackageFixture();
        var mcp = scenario switch
        {
            "malformed" => "{",
            "oversized" => $$"""{"mcpServers":{"s":{"type":"stdio"},"pad":"{{new string('x', 40 * 1024)}}"}""",
            "array-root" => "[]",
            "no-servers" => """{"other":{}}""",
            "servers-not-object" => """{"mcpServers":"nope"}""",
            "entry-not-object" => """{"mcpServers":{"s":"x"}}""",
            "missing-type" => """{"mcpServers":{"s":{"command":"run"}}}""",
            "number-type" => """{"mcpServers":{"s":{"type":3}}}""",
            "empty-type" => """{"mcpServers":{"s":{"type":""}}}""",
            _ => """{"mcpServers":{"":{"type":"stdio"}}}"""
        };
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(), PluginTestEntry.Text("mcp.json", mcp));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.McpInvalid, error.Code);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("not-zip")]
    public async Task UnreadablePackageIsRejected(string scenario)
    {
        using var fixture = new PluginPackageFixture();
        var path = scenario == "missing"
            ? Path.Combine(fixture.Root, "absent.mossplugin")
            : fixture.WriteFile("package.mossplugin", "not a zip archive"u8.ToArray());
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.Unreadable, error.Code);
    }

    [Fact]
    public async Task CancellationPropagatesUnwrapped()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ZipPluginPackagePreviewer().PreviewAsync(path, new CancellationToken(canceled: true)));
    }

    private static async Task<PluginPackageException> RejectedAsync(string path, PluginPackageFixture fixture) =>
        await Assert.ThrowsAsync<PluginPackageException>(() =>
            new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token));
}
