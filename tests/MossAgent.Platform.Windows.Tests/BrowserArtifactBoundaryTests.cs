using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class BrowserArtifactBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignTaskRootIsRejectedBeforeBrowserSideEffects(bool download)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        var context = fixture.Context with { ArtifactDirectory = Path.Combine(fixture.Files.ArtifactParent, Guid.NewGuid().ToString("N")) };
        var result = await fixture.ExecuteAsync(download, context);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.Browser.Captures + fixture.Browser.Downloads);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Files.ArtifactParent));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JunctionTaskRootCannotCaptureDownloadOrTouchOutsideSentinel(bool download)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Files.Root, "outside")).FullName;
        var sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside sentinel", fixture.Files.Token);
        await TestDirectoryJunction.CreateAsync(fixture.Files.ArtifactRoot, outside, fixture.Files.Token);
        try
        {
            var result = await fixture.ExecuteAsync(download);
            Assert.Equal("directory_create_unsafe", result.ErrorCode);
            Assert.Equal(0, fixture.Browser.Captures + fixture.Browser.Downloads);
            Assert.Equal(sentinel, Assert.Single(Directory.GetFiles(outside)));
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinel, fixture.Files.Token));
        }
        finally { Directory.Delete(fixture.Files.ArtifactRoot); }
    }
}
