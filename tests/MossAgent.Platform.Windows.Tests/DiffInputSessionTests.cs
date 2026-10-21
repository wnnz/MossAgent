using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputSessionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4 * 1024 * 1024)]
    public async Task ImmutableInputStaysPinnedAndIsRemovedThroughOriginalHandle(int size)
    {
        using var fixture = new OwnedArtifactFixture();
        var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator());
        var bytes = new byte[size];
        if (size > 0) bytes[^1] = 123;
        var session = await factory.CreateAsync(fixture.Context, bytes, fixture.Token);
        var path = session.Path;
        try
        {
            await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var copy = new byte[size];
            await reader.ReadExactlyAsync(copy, fixture.Token);
            Assert.Equal(bytes, copy);
            Assert.Throws<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete));
            Assert.Throws<IOException>(() => File.Delete(path));
            Assert.Throws<IOException>(() => File.Move(path, path + ".foreign"));
            Assert.Throws<IOException>(() => Directory.Move(fixture.ArtifactRoot, fixture.ArtifactRoot + ".foreign"));
        }
        finally { session.Dispose(); }
        session.Dispose();
        Assert.False(File.Exists(path));
        fixture.AssertNoArtifacts();
    }

    [Fact]
    public async Task InvalidTaskAndOversizedInputCannotCreateDirectoriesOrFiles()
    {
        using var fixture = new OwnedArtifactFixture();
        var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator());
        var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => factory.CreateAsync(fixture.Context,
            new byte[IDiffInputFileSessionFactory.MaximumBytes + 1], fixture.Token));
        Assert.Equal("diff_input_too_large", error.ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.CreateAsync(fixture.Context with
        { ArtifactDirectory = Path.Combine(fixture.ArtifactParent, Guid.NewGuid().ToString("N")) }, new byte[1], fixture.Token));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.CreateAsync(fixture.Context, new byte[1], cancellation.Token));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactParent));
    }
}
