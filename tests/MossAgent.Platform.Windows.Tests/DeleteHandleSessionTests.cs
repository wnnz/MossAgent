using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DeleteHandleSessionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileOrRecursiveTreeDeletesOnlyPreparedObjects(bool directory)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var parent = fixture.DirectoryFor("parent");
        var path = directory ? fixture.DirectoryFor("parent/tree") : fixture.FileFor(parent);
        if (directory)
        {
            fixture.FileFor(path, "一.txt");
            fixture.FileFor(fixture.DirectoryFor("parent/tree/nested"), "two.txt");
        }
        using var session = new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], true, false, Token);
        Assert.Equal(directory ? 4 : 1, session.PlannedCount);
        Assert.ThrowsAny<IOException>(() => File.Move(directory ? Path.Combine(path, "一.txt") : path, path + ".moved"));
        Assert.ThrowsAny<IOException>(() => Directory.Move(parent, parent + ".moved"));
        await session.DeleteAsync(_ => Task.CompletedTask, Token);
        Assert.Equal(session.PlannedCount, session.DeletedCount);
        Assert.False(Path.Exists(path));
        Assert.True(Directory.Exists(parent));
    }

    [Fact]
    public async Task PinnedFileRejectsConcurrentWriterRenameAndDeleteUntilReleased()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.FileFor(fixture.Root);
        using (var session = new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], false, true, Token))
        {
            Assert.ThrowsAny<IOException>(() => File.WriteAllText(path, "external"));
            Assert.ThrowsAny<IOException>(() => File.Delete(path));
            Assert.ThrowsAny<IOException>(() => File.Move(path, path + ".renamed"));
            Assert.NotNull(session.FileContent);
        }
        await File.WriteAllTextAsync(path, "external after dispose", Token);
        Assert.Equal("external after dispose", await File.ReadAllTextAsync(path, Token));
    }

    [Fact]
    public async Task NewChildAfterPreflightSurvivesDirectoryRemovalFailure()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.DirectoryFor("tree");
        fixture.FileFor(path, "old.txt");
        using var session = new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], true, false, Token);
        var newcomer = fixture.FileFor(path, "new.txt");
        await Assert.ThrowsAnyAsync<IOException>(() => session.DeleteAsync(_ => Task.CompletedTask, Token));
        Assert.Equal(1, session.DeletedCount);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(newcomer, Token));
        Assert.False(File.Exists(Path.Combine(path, "old.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreflightNonrecursiveOrLockedChildNeverDeletesAnything(bool locked)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.DirectoryFor("tree");
        var child = fixture.FileFor(path);
        using var reader = locked ? File.Open(child, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        var exception = Assert.Throws<FileDeleteSessionException>(() =>
            new WindowsPathDeleteSessionFactory().Create(path, [fixture.Root], locked, false, Token));
        Assert.Equal(locked ? "file_delete_unsafe" : "directory_not_empty", exception.ErrorCode);
        Assert.True(File.Exists(child));
    }

    [Fact]
    public void MissingParentIsNotCreatedAndRootOrRootAncestorCannotBeRemoved()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var factory = new WindowsPathDeleteSessionFactory();
        var missing = Path.Combine(fixture.Root, "missing");
        Assert.Throws<FileDeleteSessionException>(() => factory.Create(Path.Combine(missing, "file.txt"), [fixture.Root], false, false, Token));
        Assert.False(Directory.Exists(missing));
        var nested = fixture.DirectoryFor("outer/authorized");
        foreach (var path in new[] { fixture.Root, Path.GetDirectoryName(nested)! })
            Assert.Throws<FileDeleteSessionException>(() => factory.Create(path, [fixture.Root, nested], true, false, Token));
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public async Task MultipleRootsPermitSecondRootAndRejectSiblingPrefix()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var first = fixture.DirectoryFor("first");
        var second = fixture.DirectoryFor("second");
        var sibling = fixture.DirectoryFor("first-escape");
        var factory = new WindowsPathDeleteSessionFactory();
        var outside = fixture.FileFor(sibling);
        Assert.Throws<FileDeleteSessionException>(() => factory.Create(outside, [first, second], false, false, Token));
        var path = fixture.FileFor(second);
        using var session = factory.Create(path, [first, second], false, false, Token);
        await session.DeleteAsync(_ => Task.CompletedTask, Token);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(outside));
    }
}
