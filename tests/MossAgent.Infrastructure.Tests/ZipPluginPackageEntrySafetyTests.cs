using MossAgent.Application.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ZipPluginPackageEntrySafetyTests
{
    [Theory]
    [InlineData("/abs/x.txt")]
    [InlineData("C:/abs/x.txt")]
    [InlineData("c:relative.txt")]
    [InlineData("a//b.txt")]
    [InlineData("a//")]
    [InlineData("..")]
    [InlineData("../x.txt")]
    [InlineData("a/../b.txt")]
    [InlineData("a/./b.txt")]
    [InlineData("a\\b.txt")]
    [InlineData("a\\b/c.txt")]
    public async Task UnsafeEntryNamesAreRejectedBeforeAnyTrust(string name)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(), PluginTestEntry.Text(name, "x"));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.EntryInvalid, error.Code);
    }

    [Theory]
    [InlineData("a.txt", "A.TXT")]
    [InlineData("docs/", "DOCS")]
    public async Task CaseVariantDuplicateNamesAreRejected(string first, string second)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(),
            PluginTestEntry.Text(first, "x"), PluginTestEntry.Text(second, "y"));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.EntryInvalid, error.Code);
    }

    [Theory]
    [InlineData(0xA1ED << 16)]
    [InlineData(0xA000 << 16)]
    [InlineData(0x400)]
    public async Task LinkAndReparseEntriesAreRejected(int attributes)
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(),
            new PluginTestEntry("linked.txt", "x"u8.ToArray(), attributes));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.EntryInvalid, error.Code);
    }

    [Fact]
    public async Task EntryCountAboveTheLimitIsRejected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage([PluginPackageFixture.Manifest(),
            .. Enumerable.Range(0, 512).Select(index => PluginTestEntry.Text($"f{index}.txt", "x"))]);
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.TooLarge, error.Code);
    }

    [Fact]
    public async Task TotalUncompressedSizeAboveTheLimitIsRejected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage([PluginPackageFixture.Manifest(),
            .. Enumerable.Range(0, 9).Select(index => PluginTestEntry.Bytes($"blob{index}.bin", 4 * 1024 * 1024))]);
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.TooLarge, error.Code);
    }

    [Fact]
    public async Task SingleFileAboveTheLimitIsRejected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(),
            PluginTestEntry.Bytes("blob.bin", 4 * 1024 * 1024 + 1));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.TooLarge, error.Code);
    }

    [Fact]
    public async Task EntryNameAboveTheLimitIsRejected()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(),
            PluginTestEntry.Text(new string('a', 261) + ".txt", "x"));
        var error = await RejectedAsync(path, fixture);
        Assert.Equal(PluginPackageErrorCode.TooLarge, error.Code);
    }

    [Fact]
    public async Task LimitsAreInclusiveForAValidFileAtTheBoundary()
    {
        using var fixture = new PluginPackageFixture();
        var path = fixture.WritePackage(PluginPackageFixture.Manifest(),
            PluginTestEntry.Bytes("boundary.bin", 4 * 1024 * 1024));
        var preview = await new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token);
        Assert.Equal(2, preview.EntryCount);
        Assert.Equal(4L * 1024 * 1024 + PluginPackageFixture.Manifest().Content!.Length,
            preview.TotalUncompressedBytes);
    }

    private static async Task<PluginPackageException> RejectedAsync(string path, PluginPackageFixture fixture) =>
        await Assert.ThrowsAsync<PluginPackageException>(() =>
            new ZipPluginPackagePreviewer().PreviewAsync(path, fixture.Token));
}
