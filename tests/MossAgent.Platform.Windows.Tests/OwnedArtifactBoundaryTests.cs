using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class OwnedArtifactBoundaryTests
{
    [Theory]
    [InlineData("foreign-task")]
    [InlineData("relative")]
    [InlineData("missing")]
    [InlineData("prefix-traversal")]
    [InlineData("extension-traversal")]
    [InlineData("empty-prefix")]
    [InlineData("long-prefix")]
    [InlineData("extension-without-dot")]
    public async Task InvalidScopeOrNameCannotCreateAnyArtifact(string scenario)
    {
        using var fixture = new OwnedArtifactFixture();
        var context = fixture.Context with
        {
            ArtifactDirectory = scenario switch
            {
                "foreign-task" => Path.Combine(fixture.ArtifactParent, Guid.NewGuid().ToString("N")),
                "relative" => "artifacts/relative",
                "missing" => null,
                _ => fixture.ArtifactRoot
            }
        };
        var prefix = scenario switch { "prefix-traversal" => "../escape", "empty-prefix" => "", "long-prefix" => new string('a', 33), _ => "edit" };
        var extension = scenario switch { "extension-traversal" => ".snapshot/escape", "extension-without-dot" => "snapshot", _ => ".snapshot" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Native.CreateAsync(context, prefix, extension, fixture.Token));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactParent));
    }

    [Fact]
    public async Task ExistingJunctionCannotCreateOrCleanUpOutsideTheTaskRoot()
    {
        using var fixture = new OwnedArtifactFixture();
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Root, "outside")).FullName;
        var sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside sentinel", fixture.Token);
        await TestDirectoryJunction.CreateAsync(fixture.ArtifactRoot, outside, fixture.Token);
        try
        {
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Native.CreateAsync(fixture.Context, "edit", ".snapshot", fixture.Token));
            Assert.Equal(sentinel, Assert.Single(Directory.GetFiles(outside)));
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinel, fixture.Token));
        }
        finally { Directory.Delete(fixture.ArtifactRoot); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterCreationClosesStagingWithoutLeavingContent(bool afterCreate)
    {
        using var fixture = new OwnedArtifactFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (!afterCreate)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Native.CreateAsync(fixture.Context, "edit", ".snapshot", cancellation.Token));
            Assert.Empty(Directory.GetFileSystemEntries(fixture.ArtifactParent));
            return;
        }
        var session = await fixture.Native.CreateAsync(fixture.Context, "edit", ".snapshot", cancellation.Token);
        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.CompleteAsync(cancellation.Token));
        }
        finally { session.Dispose(); }
        Assert.False(session.Stream.CanRead);
        fixture.AssertNoArtifacts();
    }
}
