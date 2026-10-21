using System.Diagnostics;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class RootObjectIdentityBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JunctionAtRootOrAncestorIsDeniedAndEarlierRootIsReleased(bool ancestor)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var first = fixture.DirectoryFor("first");
        var outside = fixture.DirectoryFor("outside");
        Directory.CreateDirectory(Path.Combine(outside, "child"));
        var link = Path.Combine(fixture.Root, "linked");
        await CreateJunctionAsync(link, outside);
        try
        {
            var factory = new WindowsRootIdentitySessionFactory();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.OpenAsync(
                [first, ancestor ? Path.Combine(link, "child") : link], TestContext.Current.CancellationToken));
            Directory.Move(first, first + "-released");
            Directory.Move(outside, outside + "-released");
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("C:relative")]
    public async Task RelativeRootCannotBecomeAnImplicitAuthorization(string root) =>
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => new WindowsRootIdentitySessionFactory()
            .OpenAsync([root], TestContext.Current.CancellationToken));

    [Fact]
    public async Task DevicePathsAndDuplicateAliasesAreDeniedWithoutLeakingRootHandles()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("authorized");
        var factory = new WindowsRootIdentitySessionFactory();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.OpenAsync(
            [@"\\?\" + root], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => factory.OpenAsync(
            [root, Path.Combine(root, ".")], TestContext.Current.CancellationToken));
        Directory.Move(root, root + "-released");
    }

    [Fact]
    public async Task DotSegmentsResolveToTheObjectActuallyAuthorized()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var root = fixture.DirectoryFor("authorized");
        using var session = await new WindowsRootIdentitySessionFactory().OpenAsync(
            [Path.Combine(root, "unused", "..")], TestContext.Current.CancellationToken);
        Assert.Equal(root, Assert.Single(session.Roots).Path);
        Assert.False(Directory.Exists(Path.Combine(root, "unused")));
    }

    private static async Task CreateJunctionAsync(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
    }
}
