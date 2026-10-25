using System.Security.Cryptography;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitStagePathsWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StageAndUnstagePathGroupAtomicallyPreservesOtherChangesAndWorksBeforeFirstCommit(bool committed)
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync(committed);
        var token = TestContext.Current.CancellationToken;
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Root, "子目录")).FullName;
        var spaced = "新 文件 [1].txt";
        await File.WriteAllTextAsync(Path.Combine(nested, "嵌套.txt"), "nested\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, spaced), "spaced\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "plain.txt"), "plain\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), "preserved\n", token);
        var group = new[] { "子目录/嵌套.txt", spaced, "plain.txt" };

        var staged = await fixture.ExecuteAsync("git.stage_paths", new { paths = group });
        Assert.True(staged.IsSuccess, staged.Summary);
        var names = await fixture.GitAsync("diff", "--cached", "--name-only", "-z");
        foreach (var file in group) Assert.Contains(file + "\0", names);
        Assert.DoesNotContain("unselected.txt", names);

        var unstaged = await fixture.ExecuteAsync("git.unstage_paths", new { paths = group });
        Assert.True(unstaged.IsSuccess, unstaged.Summary);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
        Assert.Equal("nested\n", await File.ReadAllTextAsync(Path.Combine(nested, "嵌套.txt"), token));
        Assert.Equal("spaced\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, spaced), token));
        Assert.Equal("preserved\n", await File.ReadAllTextAsync(Path.Combine(fixture.Root, "unselected.txt"), token));
        Assert.Equal(2, fixture.ApprovalRequests);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task DuplicateAndAliasedElementsStageTheFileOnce()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "dup.txt"), "content\n", token);
        var staged = await fixture.ExecuteAsync("git.stage_paths",
            new { paths = new[] { "dup.txt", "./dup.txt", "dup.txt" } });
        Assert.True(staged.IsSuccess, staged.Summary);
        Assert.Equal("dup.txt", (await fixture.GitAsync("diff", "--cached", "--name-only")).Trim());
    }

    [Fact]
    public async Task UnmatchedElementFailsTheCommandWithoutPartiallyStagingTheGroup()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "ok.txt"), "ok\n", token);
        var index = Path.Combine(fixture.Root, ".git", "index");
        var before = await File.ReadAllBytesAsync(index, token);
        var result = await fixture.ExecuteAsync("git.stage_paths",
            new { paths = new[] { "ok.txt", "missing.txt" } });
        Assert.False(result.IsSuccess);
        Assert.Equal("git_failed", result.ErrorCode);
        Assert.Equal(before, await File.ReadAllBytesAsync(index, token));
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
        Assert.Equal(1, fixture.ApprovalRequests);
    }

    [Theory]
    [InlineData("git.stage_paths")]
    [InlineData("git.unstage_paths")]
    public async Task UnapprovedGroupLeavesTheIndexUntouchedAndCompletesAudit(string tool)
    {
        using var fixture = new GitIndexFixture { Approved = false };
        await fixture.InitializeAsync();
        var index = Path.Combine(fixture.Root, ".git", "index");
        var before = SHA256.HashData(await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken));
        var result = await fixture.ExecuteAsync(tool, new { paths = new[] { "tracked.txt" } });
        Assert.Equal("approval_denied", result.ErrorCode);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken)));
        Assert.Equal("approval_denied", fixture.Audit.Completion?.ErrorCode);
    }
}
