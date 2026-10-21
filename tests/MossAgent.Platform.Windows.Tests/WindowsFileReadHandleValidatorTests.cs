using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WindowsFileReadHandleValidatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealHandle_AllowsMultipleRootsAndCaseAliases(bool upperCase)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var first = fixture.DirectoryFor("first");
        var second = fixture.DirectoryFor("second");
        var path = fixture.FileFor(second);
        using var stream = FileSnapshotReader.Open(upperCase ? path.ToUpperInvariant() : path);
        new WindowsFileReadHandleValidator().Validate(stream, [first, upperCase ? second.ToUpperInvariant() : second]);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("outside")]
    [InlineData("prefix")]
    [InlineData("relative")]
    [InlineData("disposed")]
    public void RealHandle_RefusesUnverifiableOrUnauthorizedReads(string scenario)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var allowed = fixture.DirectoryFor("allowed");
        var path = fixture.FileFor(fixture.DirectoryFor(scenario == "prefix" ? "allowed-sibling" : "outside"));
        using var stream = FileSnapshotReader.Open(path);
        if (scenario == "disposed") stream.Dispose();
        var roots = scenario == "empty" ? Array.Empty<string>() : new[] { scenario == "relative" ? "." : allowed };
        var error = Assert.Throws<UnauthorizedAccessException>(() => new WindowsFileReadHandleValidator().Validate(stream, roots));
        Assert.DoesNotContain(fixture.Root, error.Message, StringComparison.OrdinalIgnoreCase);
        if (scenario != "disposed") Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void RealHandle_AllowsLongPaths()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("allowed");
        var deep = Directory.CreateDirectory(Path.Combine(root, new string('a', 120), new string('b', 120))).FullName;
        using var stream = FileSnapshotReader.Open(fixture.FileFor(deep));
        new WindowsFileReadHandleValidator().Validate(stream, [root]);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task SwitchedJunction_StringRecheckPassesButOpenedOutsideHandleIsRefused()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("allowed");
        var inside = fixture.DirectoryFor("allowed/inside");
        var outside = fixture.DirectoryFor("outside");
        var insidePath = fixture.FileFor(inside);
        var outsidePath = fixture.FileFor(outside);
        await File.WriteAllTextAsync(insidePath, "inside marker", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(outsidePath, "outside marker", TestContext.Current.CancellationToken);
        var link = Path.Combine(root, "link");
        var path = Path.Combine(link, "file.txt");
        await TestDirectoryJunction.CreateAsync(link, outside, TestContext.Current.CancellationToken);
        try
        {
            using var openedOutside = FileSnapshotReader.Open(path);
            Directory.Delete(link);
            await TestDirectoryJunction.CreateAsync(link, inside, TestContext.Current.CancellationToken);
            var context = new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.ReadOnly);
            Assert.Equal(path, new AuthorizedPathResolver().Resolve(path, context));
            Assert.Equal("inside marker", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            using (var unguardedReader = new StreamReader(openedOutside, leaveOpen: true))
                Assert.Equal("outside marker", await unguardedReader.ReadToEndAsync(TestContext.Current.CancellationToken));
            openedOutside.Position = 0;
            Assert.Equal(0, openedOutside.Position);
            Assert.Throws<UnauthorizedAccessException>(() =>
                new WindowsFileReadHandleValidator().Validate(openedOutside, context.AuthorizedRoots));
            Assert.Equal(0, openedOutside.Position);
            using var nowInside = FileSnapshotReader.Open(path, context with { ReadHandleValidator = new WindowsFileReadHandleValidator() });
            Assert.Equal(0, nowInside.Position);
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }

    [Fact]
    public async Task SubstitutedAuthorizedRoot_DoesNotGrantItsNewTarget()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var outside = fixture.DirectoryFor("outside");
        var path = fixture.FileFor(outside);
        var root = Path.Combine(fixture.Root, "root-link");
        await TestDirectoryJunction.CreateAsync(root, outside, TestContext.Current.CancellationToken);
        try
        {
            using var stream = FileSnapshotReader.Open(path);
            Assert.Throws<UnauthorizedAccessException>(() => new WindowsFileReadHandleValidator().Validate(stream, [root]));
        }
        finally { Directory.Delete(root); }
    }
}
