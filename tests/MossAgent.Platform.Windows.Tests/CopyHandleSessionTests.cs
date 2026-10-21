using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class CopyHandleSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationParentLeaseRemainsPinnedUntilWriterAndReportsCreatedDirectoryOnCancellation(bool cancel)
    {
        using var fixture = new CopyHandleFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var parent = Path.Combine(fixture.DestinationDirectory, "new-parent");
        var checkedLease = false;
        var context = fixture.Context with { WorkspaceGuard = new CallbackCopyWorkspaceGuard(() =>
        {
            if (!Directory.Exists(parent)) return;
            Assert.ThrowsAny<IOException>(() => Directory.Move(parent, parent + ".moved"));
            checkedLease = true;
            if (cancel) cancellation.Cancel();
        }) };
        var result = await fixture.CopyAsync(destination: Path.Combine(parent, "new.txt"), context: context, token: cancellation.Token);
        Assert.True(checkedLease);
        Assert.Equal(!cancel, result.IsSuccess);
        Assert.Equal(!cancel, File.Exists(Path.Combine(parent, "new.txt")));
        if (cancel)
        {
            Assert.Equal("tool_cancelled", result.ErrorCode);
            Assert.Contains("新建 1 个目录", result.Summary);
        }
    }

    [Fact]
    public async Task ReadonlySourceContentCanBeCopiedWithoutChangingSourceAttributes()
    {
        using var fixture = new CopyHandleFixture();
        File.SetAttributes(fixture.Source, FileAttributes.ReadOnly);
        try
        {
            var result = await fixture.CopyAsync();
            Assert.True(result.IsSuccess, result.Summary);
            Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Destination, fixture.Token));
            Assert.True((File.GetAttributes(fixture.Source) & FileAttributes.ReadOnly) != 0);
        }
        finally { File.SetAttributes(fixture.Source, FileAttributes.Normal); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileOrWholeTreeCopiesBinaryContentsAndEmptyDirectories(bool directory)
    {
        using var fixture = new CopyHandleFixture();
        Directory.CreateDirectory(Path.Combine(fixture.SourceDirectory, "empty"));
        var binary = Enumerable.Range(0, 256).Select(index => (byte)index).ToArray();
        await File.WriteAllBytesAsync(Path.Combine(fixture.SourceDirectory, "binary.bin"), binary, fixture.Token);
        var target = directory ? Path.Combine(fixture.DestinationDirectory, "tree") : fixture.Destination;
        var result = await fixture.CopyAsync(directory ? fixture.SourceDirectory : fixture.Source, target);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(directory ? Path.Combine(target, "file.txt") : target, fixture.Token));
        if (directory)
        {
            Assert.True(Directory.Exists(Path.Combine(target, "empty")));
            Assert.Equal(binary, await File.ReadAllBytesAsync(Path.Combine(target, "binary.bin"), fixture.Token));
        }
        Assert.All(fixture.Sessions.Last!.Entries, entry => Assert.True(entry.Stream is null || !entry.Stream.CanRead));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverwriteMustBeExplicitAndNeverTruncatesOnRefusal(bool overwrite)
    {
        using var fixture = new CopyHandleFixture();
        await File.WriteAllTextAsync(fixture.Destination, "original target", fixture.Token);
        var result = await fixture.CopyAsync(overwrite: overwrite);
        Assert.Equal(overwrite, result.IsSuccess);
        if (!overwrite) Assert.Equal("copy_target_exists", result.ErrorCode);
        Assert.Equal(overwrite ? "handle fixture" : "original target", await File.ReadAllTextAsync(fixture.Destination, fixture.Token));
    }

    [Fact]
    public async Task PreviouslyObservedDestinationExternalEditIsPreserved()
    {
        using var fixture = new CopyHandleFixture();
        await File.WriteAllTextAsync(fixture.Destination, "observed", fixture.Token);
        var observed = await FileSnapshotReader.ReadAsync(fixture.Destination, fixture.Token, fixture.Context);
        fixture.Context.FileSnapshots.Record(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Destination), observed);
        await File.WriteAllTextAsync(fixture.Destination, "external edit", fixture.Token);
        var result = await fixture.CopyAsync();
        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Equal("external edit", await File.ReadAllTextAsync(fixture.Destination, fixture.Token));
    }

    [Fact]
    public async Task AuthorizedRootCanBeCopiedToAnotherAuthorizedRoot()
    {
        using var fixture = new CopyHandleFixture();
        var result = await fixture.CopyAsync(fixture.SourceDirectory, fixture.DestinationDirectory,
            context: fixture.Context with { AuthorizedRoots = [fixture.SourceDirectory, fixture.DestinationDirectory] });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(Path.Combine(fixture.DestinationDirectory, "file.txt"), fixture.Token));
    }

    [Fact]
    public async Task PinnedSourceCannotBeRenamedOrChangedAndNewEntriesAreNotSilentlyIncluded()
    {
        using var fixture = new CopyHandleFixture();
        using (var session = new WindowsFileTreeReadSessionFactory().Create(fixture.SourceDirectory, [fixture.Authorized], fixture.Token))
        {
            Assert.ThrowsAny<IOException>(() => Directory.Move(fixture.SourceDirectory, fixture.SourceDirectory + ".moved"));
            Assert.ThrowsAny<IOException>(() => File.WriteAllText(fixture.Source, "external"));
            await File.WriteAllTextAsync(Path.Combine(fixture.SourceDirectory, "new.txt"), "later", fixture.Token);
            Assert.DoesNotContain(session.Entries, entry => entry.RelativePath == "new.txt");
        }
        await File.WriteAllTextAsync(fixture.Source, "released", fixture.Token);
    }
}
