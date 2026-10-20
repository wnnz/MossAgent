using System.Security.Cryptography;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitIndexWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StageAndUnstageOneLiteralFilePreservesOtherChangesAndWorksBeforeFirstCommit(bool committed)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync(committed);
        var token = TestContext.Current.CancellationToken;
        var name = "新文件 [1].txt";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, name), "selected\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), "preserved\n", token);
        var head = committed ? await fixture.GitAsync("rev-parse", "HEAD") : null;

        var staged = await fixture.ExecuteAsync("git.stage_file", new { path = name });
        Assert.True(staged.IsSuccess, staged.Summary);
        Assert.Equal(name + "\0", await fixture.GitAsync("diff", "--cached", "--name-only", "-z"));
        var unstaged = await fixture.ExecuteAsync("git.unstage_file", new { path = name });
        Assert.True(unstaged.IsSuccess, unstaged.Summary);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only", "-z"));
        Assert.Equal("selected\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, name), token));
        Assert.Equal("preserved\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), token));
        if (committed) Assert.Equal(head, await fixture.GitAsync("rev-parse", "HEAD"));
        Assert.Equal(2, fixture.ApprovalRequests);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task StageAndUnstageRenameUsesBothLiteralPathsAndDoesNotRestoreWorkingFiles()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var destination = "renamed.txt";
        File.Move(Path.Combine(fixture.Root, "tracked.txt"), Path.Combine(fixture.Root, destination));
        Assert.True((await fixture.ExecuteAsync("git.stage_file", new { path = destination, originalPath = "tracked.txt" })).IsSuccess);
        Assert.Contains("R100\0tracked.txt\0renamed.txt\0", await fixture.GitAsync("diff", "--cached", "--name-status", "-z"));
        Assert.True((await fixture.ExecuteAsync("git.unstage_file", new { path = destination, originalPath = "tracked.txt" })).IsSuccess);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "tracked.txt")));
        Assert.True(File.Exists(Path.Combine(fixture.Root, destination)));
    }

    [Fact]
    public async Task DeletedTrackedFileCanBeStagedAndUnstagedWithoutRecreatingIt()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        File.Delete(Path.Combine(fixture.Root, "tracked.txt"));
        Assert.True((await fixture.ExecuteAsync("git.stage_file", new { path = "tracked.txt" })).IsSuccess);
        Assert.Contains("D\ttracked.txt", await fixture.GitAsync("diff", "--cached", "--name-status"));
        Assert.True((await fixture.ExecuteAsync("git.unstage_file", new { path = "tracked.txt" })).IsSuccess);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "tracked.txt")));
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
    }

    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task UnapprovedOperationLeavesTheIndexUntouchedAndCompletesAudit(string tool)
    {
        using var fixture = new GitIndexFixture { Approved = false };
        await fixture.InitializeAsync();
        var index = Path.Combine(fixture.Root, ".git", "index");
        var before = SHA256.HashData(await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken));
        var result = await fixture.ExecuteAsync(tool, new { path = "tracked.txt" });
        Assert.Equal("approval_denied", result.ErrorCode);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken)));
        Assert.Equal("approval_denied", fixture.Audit.Completion?.ErrorCode);
    }
}
