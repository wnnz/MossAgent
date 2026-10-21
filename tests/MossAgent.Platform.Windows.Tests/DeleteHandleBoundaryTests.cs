using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DeleteHandleBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParentOrDescendantJunctionRefusedWithoutTouchingSentinels(bool descendant)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var authorized = fixture.DirectoryFor("authorized");
        var outside = fixture.DirectoryFor("outside");
        var sentinel = fixture.FileFor(outside);
        var tree = fixture.DirectoryFor("authorized/tree");
        var link = Path.Combine(tree, "link");
        await TestDirectoryJunction.CreateAsync(link, outside, Token);
        try
        {
            var target = descendant ? tree : Path.Combine(link, "file.txt");
            Assert.Throws<FileDeleteSessionException>(() => new WindowsPathDeleteSessionFactory().Create(target, [authorized], true, false, Token));
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(sentinel, Token));
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TreeDepthOrCountLimitIsCheckedBeforeMutation(bool depth)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var tree = fixture.DirectoryFor("tree");
        if (depth)
        {
            var parent = tree;
            for (var index = 0; index < 65; index++) parent = Directory.CreateDirectory(Path.Combine(parent, "d")).FullName;
        }
        else
        {
            for (var index = 0; index < 4096; index++) File.WriteAllText(Path.Combine(tree, $"{index}.txt"), "x");
        }
        var exception = Assert.Throws<FileDeleteSessionException>(() => new WindowsPathDeleteSessionFactory().Create(tree, [fixture.Root], true, false, Token));
        Assert.Equal("file_delete_limit", exception.ErrorCode);
        Assert.Equal(depth ? 65 : 4096, Directory.EnumerateFileSystemEntries(tree, "*", SearchOption.AllDirectories).Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileOnlyRefusesDirectoryAndExternalNamedStream(bool directory)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = directory ? fixture.DirectoryFor("target") : fixture.FileFor(fixture.Root);
        if (!directory) await File.WriteAllTextAsync(path + ":external", "preserve", Token);
        Assert.Throws<FileDeleteSessionException>(() => new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], false, true, Token));
        Assert.True(Path.Exists(path));
        if (!directory) Assert.Equal("preserve", await File.ReadAllTextAsync(path + ":external", Token));
    }

    [Fact]
    public async Task ExplicitFileDeletionIncludesNamedStreams()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.FileFor(fixture.Root);
        await File.WriteAllTextAsync(path + ":external", "delete entire file", Token);
        using var session = new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], false, false, Token);
        await session.DeleteAsync(_ => Task.CompletedTask, Token);
        Assert.False(File.Exists(path));
    }
}
