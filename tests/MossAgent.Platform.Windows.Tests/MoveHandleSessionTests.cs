using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class MoveHandleSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameVolumeFileAndDirectoryMoveRetainsContents(bool directory)
    {
        using var fixture = new MoveHandleFixture();
        var source = directory ? fixture.SourceParent : fixture.Source;
        var result = await fixture.MoveAsync(source);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.False(Path.Exists(source));
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(directory ? Path.Combine(fixture.Target, "file.txt") : fixture.Target, fixture.Token));
        Assert.True(fixture.Sessions.Last!.Moved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingTargetRequiresExplicitOverwrite(bool overwrite)
    {
        using var fixture = new MoveHandleFixture();
        await File.WriteAllTextAsync(fixture.Target, "old target", fixture.Token);
        var result = await fixture.MoveAsync(overwrite: overwrite);
        Assert.Equal(overwrite, result.IsSuccess);
        Assert.Equal(!overwrite, File.Exists(fixture.Source));
        Assert.Equal(overwrite ? "handle fixture" : "old target", await File.ReadAllTextAsync(fixture.Target, fixture.Token));
        if (!overwrite) Assert.Equal("move_target_exists", result.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedObservedContentRefusesMove(bool destination)
    {
        using var fixture = new MoveHandleFixture();
        var path = destination ? fixture.Target : fixture.Source;
        if (destination) await File.WriteAllTextAsync(path, "observed", fixture.Token);
        var observed = await FileSnapshotReader.ReadAsync(path, fixture.Token, fixture.Context);
        fixture.Context.FileSnapshots.Record(fixture.Context.TaskId, FileSnapshotReader.CacheKey(path), observed);
        await File.WriteAllTextAsync(path, "external change", fixture.Token);
        var result = await fixture.MoveAsync(overwrite: true);
        Assert.Equal("file_changed", result.ErrorCode);
        Assert.True(File.Exists(fixture.Source));
        Assert.Equal("external change", await File.ReadAllTextAsync(path, fixture.Token));
    }

    [Fact]
    public async Task MultipleAuthorizedRootsAndMissingParentAreSupported()
    {
        using var fixture = new MoveHandleFixture();
        var target = Path.Combine(fixture.Outside, "new", "nested", "moved.bin");
        var context = fixture.Context with { AuthorizedRoots = [fixture.Authorized, fixture.Outside] };
        var result = await fixture.MoveAsync(target: target, context: context);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(target, fixture.Token));
        Assert.False(File.Exists(fixture.Source));
    }

    [Fact]
    public void CrossVolumeIsExplicitlyRefusedBeforeOpeningSource()
    {
        using var fixture = new MoveHandleFixture();
        var differentDrive = Path.GetPathRoot(fixture.Source)![0] == 'C' ? "D:\\" : "C:\\";
        var error = Assert.Throws<FileMutationSessionException>(() => new WindowsPathMoveSessionFactory()
            .Create(fixture.Source, Path.Combine(differentDrive, "unavailable", "target"), [fixture.Authorized], fixture.Token));
        Assert.Equal("file_move_cross_volume", error.ErrorCode);
        Assert.True(File.Exists(fixture.Source));
    }

    [Fact]
    public async Task NativeCommitRefusesTargetThatAppearsAfterSnapshot()
    {
        using var fixture = new MoveHandleFixture();
        using var session = new WindowsPathMoveSessionFactory().Create(fixture.Source, fixture.Target, [fixture.Authorized], fixture.Token);
        await File.WriteAllTextAsync(fixture.Target, "late target", fixture.Token);
        Assert.False(await session.TryCommitAsync(null, _ => Task.CompletedTask, fixture.Token));
        Assert.False(session.Moved);
        Assert.True(File.Exists(fixture.Source));
        Assert.Equal("late target", await File.ReadAllTextAsync(fixture.Target, fixture.Token));
    }

    [Fact]
    public async Task SourceHandlesBlockWritersAndRenameUntilReleased()
    {
        using var fixture = new MoveHandleFixture();
        using (var session = new WindowsPathMoveSessionFactory().Create(fixture.Source, fixture.Target, [fixture.Authorized], fixture.Token))
        {
            Assert.Throws<IOException>(() => File.OpenWrite(fixture.Source));
            Assert.Throws<IOException>(() => Directory.Move(fixture.SourceParent, fixture.SourceParent + "-renamed"));
            Assert.Throws<IOException>(() => File.Move(fixture.Source, fixture.Source + "-renamed"));
        }
        Assert.True((await fixture.MoveAsync()).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservedFileReplacedByDirectoryOrRemovedCannotBeSilentlyMoved(bool target)
    {
        using var fixture = new MoveHandleFixture();
        var path = target ? fixture.Target : fixture.Source;
        if (target) await File.WriteAllTextAsync(path, "observed target", fixture.Token);
        fixture.Context.FileSnapshots.Record(fixture.Context.TaskId, FileSnapshotReader.CacheKey(path),
            await FileSnapshotReader.ReadAsync(path, fixture.Token, fixture.Context));
        File.Delete(path);
        if (!target) Directory.CreateDirectory(path);
        var result = await fixture.MoveAsync(source: target ? fixture.SourceParent : fixture.Source);
        Assert.Equal("file_changed", result.ErrorCode);
        Assert.True(Directory.Exists(target ? fixture.SourceParent : fixture.Source));
        Assert.False(Path.Exists(fixture.Target));
    }

    [Fact]
    public async Task SuccessfulMoveInvalidatesBothObservedFileVersions()
    {
        using var fixture = new MoveHandleFixture();
        await File.WriteAllTextAsync(fixture.Target, "original", fixture.Token);
        foreach (var path in new[] { fixture.Source, fixture.Target })
            fixture.Context.FileSnapshots.Record(fixture.Context.TaskId, FileSnapshotReader.CacheKey(path),
                await FileSnapshotReader.ReadAsync(path, fixture.Token, fixture.Context));
        Assert.True((await fixture.MoveAsync(overwrite: true)).IsSuccess);
        Assert.Null(fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Source)));
        Assert.Null(fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Target)));
    }
}
