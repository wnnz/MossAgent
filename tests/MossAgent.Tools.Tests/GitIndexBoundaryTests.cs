using MossAgent.Domain;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitIndexBoundaryTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData(".git/index")]
    [InlineData(".")]
    [InlineData("tracked.txt:stream")]
    [InlineData("tracked.txt.")]
    [InlineData("NUL")]
    [InlineData("con.txt")]
    [InlineData("folder/COM1.log")]
    [InlineData("LPT9")]
    [InlineData("COM¹.txt")]
    [InlineData("con .txt")]
    public async Task InvalidFileScopeIsDeniedBeforeApproval(string path)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.stage_file", new { path });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
    }

    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task ReadOnlyAndMalformedRequestsNeverReachApproval(string tool)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var denied = await fixture.ExecuteAsync(tool, new { path = "tracked.txt" }, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.Equal("capability_denied", denied.ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, new { path = "tracked.txt", unexpected = true })).ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task StandaloneExternalGitMetadataCannotBeMutatedWithoutExplicitAuthorization()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var metadata = fixture.Root + "-metadata";
        var pointer = Path.Combine(fixture.Root, ".git");
        Directory.Move(pointer, metadata);
        try
        {
            await File.WriteAllTextAsync(pointer, "gitdir: " + metadata + "\n", TestContext.Current.CancellationToken);
            var index = await File.ReadAllBytesAsync(Path.Combine(metadata, "index"), TestContext.Current.CancellationToken);
            var denied = await fixture.ExecuteAsync("git.stage_file", new { path = "tracked.txt" });
            Assert.Equal("path_denied", denied.ErrorCode);
            Assert.Equal(index, await File.ReadAllBytesAsync(Path.Combine(metadata, "index"), TestContext.Current.CancellationToken));
            var allowed = await fixture.ExecuteAsync("git.stage_file", new { path = "tracked.txt" }, fixture.Context with { AuthorizedRoots = [fixture.Root, metadata] });
            Assert.True(allowed.IsSuccess, allowed.Summary);
        }
        finally { File.Delete(pointer); Directory.Move(metadata, pointer); }
    }

    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task NestedFolderAuthorizationDoesNotGrantItsParentRepository(string tool)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Root, "nested")).FullName;
        await File.WriteAllTextAsync(Path.Combine(nested, "selected.txt"), "preserved\n", token);
        if (tool == "git.unstage_file") await fixture.GitAsync("add", "--", "nested/selected.txt");
        var index = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, ".git", "index"), token);
        var context = fixture.Context with { WorkingDirectory = nested, AuthorizedRoots = [nested] };
        var denied = await fixture.ExecuteAsync(tool, new { path = "selected.txt" }, context);
        Assert.Equal("path_denied", denied.ErrorCode);
        Assert.Equal(index, await File.ReadAllBytesAsync(Path.Combine(fixture.Root, ".git", "index"), token));
        var allowed = await fixture.ExecuteAsync(tool, new { path = "selected.txt" }, context with { AuthorizedRoots = [nested, fixture.Root] });
        Assert.True(allowed.IsSuccess, allowed.Summary);
        Assert.Equal("preserved\n", await File.ReadAllTextAsync(Path.Combine(nested, "selected.txt"), token));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData(".git/index")]
    [InlineData(".")]
    public async Task InvalidRenameSourceIsDeniedBeforeApproval(string originalPath)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.unstage_file", new { path = "tracked.txt", originalPath });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
    }
}
