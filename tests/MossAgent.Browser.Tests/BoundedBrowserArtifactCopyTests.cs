using MossAgent.Browser.Artifacts;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class BoundedBrowserArtifactCopyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnderLimitCopiesEveryByte()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("sdk download payload");
        await using var source = new MemoryStream(payload);
        await using var destination = new MemoryStream();

        await BoundedBrowserArtifactCopy.CopyAsync(source, destination, 1024, Token);

        Assert.Equal(payload, destination.ToArray());
    }

    [Fact]
    public async Task ExactlyAtLimitCopiesWithoutThrowing()
    {
        var payload = new byte[65 * 1024 + 7];
        new Random(42).NextBytes(payload);
        await using var source = new MemoryStream(payload);
        await using var destination = new MemoryStream();

        await BoundedBrowserArtifactCopy.CopyAsync(source, destination, payload.LongLength, Token);

        Assert.Equal(payload, destination.ToArray());
    }

    [Theory]
    [InlineData(128L, 129L)]
    [InlineData(64 * 1024L, 70 * 1024L)]
    [InlineData(1L, 200 * 1024L)]
    public async Task OverLimitThrowsAndWritesOnlyTheBoundedPrefix(long limit, long sourceLength)
    {
        await using var source = new RepeatingReadStream(sourceLength);
        await using var destination = new MemoryStream();

        var error = await Assert.ThrowsAsync<FileMutationSessionException>(
            () => BoundedBrowserArtifactCopy.CopyAsync(source, destination, limit, Token));

        Assert.Equal("browser_artifact_too_large", error.ErrorCode);
        Assert.False(error.HasSideEffects);
        Assert.Equal(limit, destination.Length);
    }

    [Fact]
    public async Task ZeroLengthSourceWritesNothing()
    {
        await using var source = new MemoryStream();
        await using var destination = new MemoryStream();

        await BoundedBrowserArtifactCopy.CopyAsync(source, destination, 0, Token);

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task CancellationPropagatesOutOfTheCopy()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync();
        await using var source = new RepeatingReadStream(1024);
        await using var destination = new MemoryStream();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BoundedBrowserArtifactCopy.CopyAsync(source, destination, 1024, cancellation.Token));
    }
}
