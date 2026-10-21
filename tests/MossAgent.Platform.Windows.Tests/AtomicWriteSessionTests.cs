using System.Text;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWriteSessionTests
{
    [Theory]
    [InlineData("change")]
    [InlineData("delete")]
    [InlineData("create")]
    public async Task ConcurrentTargetChangeIsPreserved(string action)
    {
        using var fixture = new AtomicWriteFixture();
        if (action != "create") await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        var error = await Assert.ThrowsAsync<FileEditException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("updated"), token);
                if (action == "delete") File.Delete(fixture.Path);
                else await File.WriteAllTextAsync(fixture.Path, "external", token);
            }, fixture.Token));
        Assert.Equal("file_changed", error.ErrorCode);
        if (action == "delete") Assert.False(File.Exists(fixture.Path));
        else Assert.Equal("external", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelledStageOrFailureNeverCommits_AndReleasesDirectories(bool cancel)
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var original = await fixture.SnapshotAsync();
        var write = fixture.Writer.WriteAsync(fixture.Path, original, fixture.Context, async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), token);
            if (cancel) { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); }
            throw new IOException("synthetic write failure");
        }, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await write);
        else await Assert.ThrowsAsync<IOException>(async () => await write);
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
        await fixture.WriteAsync(original);
    }

    [Fact]
    public async Task TargetLockIsRespected_AndDoesNotTruncateTarget()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        using (var held = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<FileWriteSessionException>(() => fixture.WriteAsync(original));
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
        await fixture.WriteAsync(original);
    }

    [Fact]
    public async Task StagingHandlePreventsExternalWritesAndDeletion()
    {
        using var fixture = new AtomicWriteFixture();
        using (var session = fixture.Sessions.Create(fixture.Path, [fixture.Root], fixture.Token))
        {
            await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("owned"), fixture.Token);
            await session.Stream.FlushAsync(fixture.Token);
            Assert.Throws<IOException>(() => File.Delete(session.TemporaryPath));
            await Assert.ThrowsAsync<IOException>(() => File.ReadAllTextAsync(session.TemporaryPath, fixture.Token));
            await Assert.ThrowsAsync<IOException>(() => File.WriteAllTextAsync(session.TemporaryPath, "external", fixture.Token));
            Assert.True(await session.TryCommitAsync(null, fixture.Token));
        }
        Assert.Equal("owned", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CommitRechecksHashAndDoesNotOverwriteChangedTarget()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        using (var session = fixture.Sessions.Create(fixture.Path, [fixture.Root], fixture.Token))
        {
            await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("updated"), fixture.Token);
            await File.WriteAllTextAsync(fixture.Path, "external", fixture.Token);
            Assert.False(await session.TryCommitAsync(original.Sha256, fixture.Token));
        }
        Assert.Equal("external", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
