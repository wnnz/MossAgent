using System.Text;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWriteConcurrencyTests
{
    [Fact]
    public async Task RevokedRootsRefuseCommitAndCleanByHandle()
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var roots = new List<string> { fixture.Root };
        var context = fixture.Context with { AuthorizedRoots = roots };
        var original = await fixture.SnapshotAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Writer.WriteAsync(
            fixture.Path, original, context, async (stream, token) =>
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes("updated"), token);
                roots.Clear();
            }, fixture.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
        await fixture.WriteAsync(await fixture.SnapshotAsync());
    }

    [Fact]
    public async Task CancellingOneWriterDoesNotCancelAnotherTaskOrDeleteItsStage()
    {
        using var left = new AtomicWriteFixture();
        using var right = new AtomicWriteFixture();
        await left.SeedAsync();
        await right.SeedAsync();
        var original = await left.SnapshotAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(left.Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = left.Writer.WriteAsync(left.Path, original, left.Context, async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), token);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        }, cancellation.Token);
        Task<MossAgent.Tools.Abstractions.FileSnapshot>? other = null;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), left.Token);
            other = left.Writer.WriteTextAsync(right.Path, "right updated", new UTF8Encoding(false),
                await right.SnapshotAsync(), right.Context, right.Token);
            Assert.False(cancelled.IsCompleted);
        }
        finally
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);
        }
        Assert.NotNull(other);
        Assert.True((await other.WaitAsync(TimeSpan.FromSeconds(5), right.Token)).Exists);
        Assert.Equal("original", await File.ReadAllTextAsync(left.Path, left.Token));
        Assert.Equal("right updated", await File.ReadAllTextAsync(right.Path, right.Token));
        left.AssertNoTemporaryFiles();
        right.AssertNoTemporaryFiles();
    }
}
