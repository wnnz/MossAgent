using System.Text;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWritePathBoundaryTests
{
    [Fact]
    public async Task MissingSubdirectoriesAreCreatedRelativeToAuthorizedRoot()
    {
        using var fixture = new AtomicWriteFixture();
        var path = Path.Combine(fixture.Root, "one", "two", "created.txt");
        var result = await fixture.Writer.WriteTextAsync(path, "new", new UTF8Encoding(false), new(null), fixture.Context, fixture.Token);
        Assert.True(result.Exists);
        Assert.Equal("new", await File.ReadAllTextAsync(path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public void NoRootsOrRootOutsidePathAreDeniedBeforeStaging()
    {
        using var fixture = new AtomicWriteFixture();
        using var outside = new AtomicWriteFixture();
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Sessions.Create(fixture.Path, [], fixture.Token));
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Sessions.Create(outside.Path, [fixture.Root], fixture.Token));
        Assert.Throws<UnauthorizedAccessException>(() => fixture.Sessions.Create(fixture.Root, [fixture.Root], fixture.Token));
        fixture.AssertNoTemporaryFiles();
        outside.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("..\\outside.txt")]
    [InlineData("file.txt:stream")]
    [InlineData("file.txt.")]
    [InlineData("file.txt ")]
    public void TraversalAndAmbiguousNamesAreDenied(string suffix)
    {
        using var fixture = new AtomicWriteFixture();
        Assert.Throws<UnauthorizedAccessException>(() =>
        {
            using var session = fixture.Sessions.Create(Path.Combine(fixture.Root, suffix), [fixture.Root], fixture.Token);
        });
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingJunctionIsRejectedEvenWhenItsTargetIsInsideRoot(bool inside)
    {
        using var fixture = new AtomicWriteFixture();
        using var outside = new AtomicWriteFixture();
        var link = Path.Combine(fixture.Root, "link");
        await TestDirectoryJunction.CreateAsync(link, inside ? fixture.Parent : outside.Parent, fixture.Token);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
            {
                using var session = fixture.Sessions.Create(Path.Combine(link, "file.txt"), [fixture.Root], fixture.Token);
            });
            fixture.AssertNoTemporaryFiles();
            outside.AssertNoTemporaryFiles();
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public async Task SecondAuthorizedRootIsUsable()
    {
        using var fixture = new AtomicWriteFixture();
        using var second = new AtomicWriteFixture();
        var context = fixture.Context with { AuthorizedRoots = [fixture.Root, second.Root] };
        await fixture.Writer.WriteTextAsync(second.Path, "second", new UTF8Encoding(false), new(null), context, fixture.Token);
        Assert.Equal("second", await File.ReadAllTextAsync(second.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
        second.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task OrdinaryWindowsPathsAcceptDifferentInputCasing()
    {
        using var fixture = new AtomicWriteFixture();
        var path = fixture.Path.ToLowerInvariant();
        using (var session = fixture.Sessions.Create(path, [fixture.Root.ToLowerInvariant()], fixture.Token))
        {
            await session.Stream.WriteAsync(Encoding.UTF8.GetBytes("casing"), fixture.Token);
            Assert.True(await session.TryCommitAsync(null, fixture.Token));
        }
        Assert.Equal("casing", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
