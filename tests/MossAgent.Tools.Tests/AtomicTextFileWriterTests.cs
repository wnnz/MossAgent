using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class AtomicTextFileWriterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExternalChangeDuringStaging_IsPreserved(bool existed)
    {
        using var fixture = new FileEditingFixture();
        if (existed) await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();

        var exception = await Assert.ThrowsAsync<FileEditException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("agent change"), token);
                await File.WriteAllTextAsync(fixture.Path, "external change", token);
            }, fixture.Token));

        Assert.Equal("file_changed", exception.ErrorCode);
        Assert.Equal("external change", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ExternalDeletionDuringStaging_DoesNotRecreateTarget()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();

        await Assert.ThrowsAsync<FileEditException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("agent change"), token);
                File.Delete(fixture.Path);
            }, fixture.Token));

        Assert.False(File.Exists(fixture.Path));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationAfterPartialStaging_NeverCommits(bool existed)
    {
        using var fixture = new FileEditingFixture();
        if (existed) await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), token);
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            }, cancellation.Token));

        Assert.Equal(existed, File.Exists(fixture.Path));
        if (existed) Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task StagingFailure_LeavesOriginalUntouched()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();

        await Assert.ThrowsAsync<IOException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, fixture.Context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), token);
                throw new IOException("synthetic staging failure");
            }, fixture.Token));

        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task TargetLockRejectsCommitWithoutTruncatingOriginal()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();
        using (var heldRead = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Writer.WriteTextAsync(
                fixture.Path, "new content", new UTF8Encoding(false), original, fixture.Context, fixture.Token));
        }

        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task SharedWriterSerializesDifferentTasksAndRejectsStaleVersion()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = fixture.Writer.WriteAsync(fixture.Path, original, fixture.Context, async (stream, token) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("first writer"), token);
        }, fixture.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        var secondContext = new ToolExecutionContext(Guid.NewGuid(), fixture.Root, [fixture.Root], fixture.Context.ApprovalPolicy);
        var second = fixture.Writer.WriteTextAsync(fixture.Path, "second writer", new UTF8Encoding(false),
            original, secondContext, fixture.Token);

        Assert.False(second.IsCompleted);
        release.SetResult();
        await first;
        await Assert.ThrowsAsync<FileEditException>(async () => await second);

        Assert.Equal("first writer", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task RevokedAuthorizationDuringStaging_RejectsCommitAndCleansTemporaryFile()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();
        var roots = new List<string> { fixture.Root };
        var context = fixture.Context with { AuthorizedRoots = roots };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("agent change"), token);
                roots.Clear();
            }, fixture.Token));

        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CancelledGateWait_NeverStagesAndDoesNotBlockLaterWrites()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var original = await fixture.SnapshotAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = fixture.Writer.WriteAsync(fixture.Path, original, fixture.Context, async (stream, token) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("first"), token);
        }, fixture.Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            var waiting = fixture.Writer.WriteTextAsync(fixture.Path, "cancelled", new UTF8Encoding(false),
                original, fixture.Context, cancellation.Token);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waiting);
            Assert.Equal("original", await fixture.ContentsAsync());
        }
        finally { release.TrySetResult(); }
        var committed = await first;
        await fixture.Writer.WriteTextAsync(fixture.Path, "final", new UTF8Encoding(false), committed,
            fixture.Context, fixture.Token);
        Assert.Equal("final", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }
}
