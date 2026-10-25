using System.Diagnostics;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitStagePathsBoundaryTests
{
    [Theory]
    [InlineData("git.stage_paths", "../outside.txt")]
    [InlineData("git.stage_paths", ".git/index")]
    [InlineData("git.stage_paths", ".")]
    [InlineData("git.stage_paths", "folder")]
    [InlineData("git.stage_paths", "NUL")]
    [InlineData("git.stage_paths", "tracked.txt.")]
    [InlineData("git.unstage_paths", "../outside.txt")]
    [InlineData("git.unstage_paths", ".git/index")]
    [InlineData("git.unstage_paths", ".")]
    [InlineData("git.unstage_paths", "folder")]
    [InlineData("git.unstage_paths", "COM1.log")]
    public async Task AnyInvalidElementRejectsTheWholeGroupBeforeApprovalWithoutSideEffects(string tool, string invalid)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(Path.Combine(fixture.Root, "folder"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "ok.txt"), "ok\n", token);
        var index = Path.Combine(fixture.Root, ".git", "index");
        var before = await File.ReadAllBytesAsync(index, token);
        var result = await fixture.ExecuteAsync(tool, new { paths = new[] { "ok.txt", invalid, "plain.txt" } });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Equal(before, await File.ReadAllBytesAsync(index, token));
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
    }

    [Theory]
    [InlineData("git.stage_paths")]
    [InlineData("git.unstage_paths")]
    public async Task ReadOnlyAndMalformedRequestsNeverReachApproval(string tool)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var denied = await fixture.ExecuteAsync(tool, new { paths = new[] { "tracked.txt" } },
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.Equal("capability_denied", denied.ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, new { path = "tracked.txt" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, new { paths = Array.Empty<string>() })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool,
            new { paths = Enumerable.Range(0, 65).Select(n => $"f{n}.txt").ToArray() })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool,
            new { paths = new object[] { "tracked.txt", 7 } })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool,
            new { paths = new[] { "tracked.txt" }, unexpected = true })).ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
    }

    [Fact]
    public async Task ElementOutsideTheRepositoryIsDeniedEvenWhenItsRootIsAuthorized()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var outside = Path.GetFullPath(fixture.Root + "-outside");
        Directory.CreateDirectory(outside);
        try
        {
            var external = Path.Combine(outside, "external.txt");
            await File.WriteAllTextAsync(external, "untouched\n", token);
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "ok.txt"), "ok\n", token);
            var index = Path.Combine(fixture.Root, ".git", "index");
            var before = await File.ReadAllBytesAsync(index, token);
            var result = await fixture.ExecuteAsync("git.stage_paths", new { paths = new[] { "ok.txt", external } },
                fixture.Context with { AuthorizedRoots = [fixture.Root, outside] });
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(before, await File.ReadAllBytesAsync(index, token));
            Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
            Assert.Equal("untouched\n", await File.ReadAllTextAsync(external, token));
        }
        finally { Directory.Delete(outside, recursive: true); }
    }

    [Theory]
    [InlineData("git.stage_paths", false)]
    [InlineData("git.stage_paths", true)]
    [InlineData("git.unstage_paths", false)]
    [InlineData("git.unstage_paths", true)]
    public async Task LinkedElementRejectsTheWholeGroupIncludingChangesDuringApproval(string tool, bool afterApproval)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var outside = Path.GetFullPath(fixture.Root + "-outside");
        var link = Path.Combine(fixture.Root, "linked");
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(parent, outside, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(outside);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(outside, "sentinel.txt"), "untouched", token);
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "ok.txt"), "ok\n", token);
            var index = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, ".git", "index"), token);
            if (afterApproval) fixture.OnApproval = () => CreateJunctionAsync(fixture.Root, link, outside, token);
            else await CreateJunctionAsync(fixture.Root, link, outside, token);
            var result = await fixture.ExecuteAsync(tool, new { paths = new[] { "ok.txt", "linked/sentinel.txt" } },
                fixture.Context with { AuthorizedRoots = [fixture.Root, outside] });
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(afterApproval ? 1 : 0, fixture.ApprovalRequests);
            Assert.Equal("untouched", await File.ReadAllTextAsync(Path.Combine(outside, "sentinel.txt"), token));
            Assert.Equal(index, await File.ReadAllBytesAsync(Path.Combine(fixture.Root, ".git", "index"), token));
        }
        finally
        {
            if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static async Task CreateJunctionAsync(string root, string link, string target, CancellationToken token)
    {
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, Path.GetFullPath(link), StringComparison.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
        var start = new ProcessStartInfo("cmd.exe")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token);
            Assert.True(process.ExitCode == 0, $"Junction fixture failed: {await stdout} {await stderr}");
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
    }
}
