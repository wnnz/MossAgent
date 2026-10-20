using System.Diagnostics;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitIndexJunctionTests
{
    [Theory]
    [InlineData("git.stage_file", false)]
    [InlineData("git.stage_file", true)]
    [InlineData("git.unstage_file", false)]
    [InlineData("git.unstage_file", true)]
    public async Task LinkedFileAncestryIsRejectedIncludingChangesDuringApproval(string tool, bool afterApproval)
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
            var index = await File.ReadAllBytesAsync(Path.Combine(fixture.Root, ".git", "index"), token);
            if (afterApproval) fixture.OnApproval = () => CreateJunctionAsync(fixture.Root, link, outside, token);
            else await CreateJunctionAsync(fixture.Root, link, outside, token);
            var result = await fixture.ExecuteAsync(tool, new { path = "linked/sentinel.txt" },
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
