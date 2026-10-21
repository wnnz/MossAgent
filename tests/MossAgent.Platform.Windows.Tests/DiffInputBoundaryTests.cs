using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputBoundaryTests
{
    [Fact]
    public async Task JunctionArtifactRootCannotCreateInputOrModifyOutsideSentinel()
    {
        using var fixture = new OwnedArtifactFixture();
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Root, "outside")).FullName;
        var sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "outside sentinel", fixture.Token);
        await TestDirectoryJunction.CreateAsync(fixture.ArtifactRoot, outside, fixture.Token);
        try
        {
            var factory = new WindowsDiffInputFileSessionFactory(new WindowsAuthorizedDirectoryCreator());
            var error = await Assert.ThrowsAsync<FileMutationSessionException>(() => factory.CreateAsync(fixture.Context, new byte[1], fixture.Token));
            Assert.Equal("directory_create_unsafe", error.ErrorCode);
            Assert.Equal(sentinel, Assert.Single(Directory.GetFiles(outside)));
            Assert.Equal("outside sentinel", await File.ReadAllTextAsync(sentinel, fixture.Token));
        }
        finally { Directory.Delete(fixture.ArtifactRoot); }
    }
}
