using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class CopyHandleBoundaryTests
{
    [Theory]
    [InlineData("junction")]
    [InlineData("locked")]
    [InlineData("ads")]
    public async Task SourcePreflightRefusesUnsupportedEntryBeforeCreatingAnyTarget(string scenario)
    {
        using var fixture = new CopyHandleFixture();
        var target = Path.Combine(fixture.DestinationDirectory, "tree");
        var link = Path.Combine(fixture.SourceDirectory, "link");
        if (scenario == "junction") await TestDirectoryJunction.CreateAsync(link, fixture.Outside, fixture.Token);
        if (scenario == "ads") await File.WriteAllTextAsync(fixture.Source + ":extra", "preserve", fixture.Token);
        using var writer = scenario == "locked" ? File.Open(fixture.Source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite) : null;
        try
        {
            var result = await fixture.CopyAsync(fixture.SourceDirectory, target);
            Assert.Equal("file_tree_unsafe", result.ErrorCode);
            Assert.False(Directory.Exists(target));
            Assert.True(File.Exists(fixture.Source));
        }
        finally { if (scenario == "junction") Directory.Delete(link); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceTreeDepthOrCountLimitRefusesWithoutCreatingTarget(bool depth)
    {
        using var fixture = new CopyHandleFixture();
        if (depth)
        {
            var path = fixture.SourceDirectory;
            for (var index = 0; index < 65; index++) path = Directory.CreateDirectory(Path.Combine(path, "d")).FullName;
        }
        else
        {
            for (var index = 0; index < 4095; index++) File.WriteAllText(Path.Combine(fixture.SourceDirectory, $"{index}.txt"), "x");
        }
        var error = Assert.Throws<FileMutationSessionException>(() => new WindowsFileTreeReadSessionFactory()
            .Create(fixture.SourceDirectory, [fixture.Authorized], fixture.Token));
        Assert.Equal("file_tree_limit", error.ErrorCode);
        Assert.False(File.Exists(fixture.Destination));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelfOrNestedDestinationIsRejectedBeforeSourceAcquisition(bool nested)
    {
        using var fixture = new CopyHandleFixture();
        var result = await fixture.CopyAsync(fixture.SourceDirectory,
            nested ? Path.Combine(fixture.SourceDirectory, "new") : fixture.SourceDirectory);
        Assert.Equal("copy_self_denied", result.ErrorCode);
        Assert.Equal(0, fixture.Sessions.Creates);
    }

    [Fact]
    public async Task NativeDestinationMetadataRefusalDoesNotTruncateOldContentOrStream()
    {
        using var fixture = new CopyHandleFixture();
        await File.WriteAllTextAsync(fixture.Destination, "old target", fixture.Token);
        await File.WriteAllTextAsync(fixture.Destination + ":extra", "old metadata", fixture.Token);
        var result = await fixture.CopyAsync();
        Assert.Equal("file_write_metadata_unsupported", result.ErrorCode);
        Assert.Equal("old target", await File.ReadAllTextAsync(fixture.Destination, fixture.Token));
        Assert.Equal("old metadata", await File.ReadAllTextAsync(fixture.Destination + ":extra", fixture.Token));
        Assert.Empty(Directory.GetFiles(fixture.Authorized, ".mossagent-*.tmp", SearchOption.AllDirectories));
    }
}
