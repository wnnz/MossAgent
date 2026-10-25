using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitHunksWorkflowTests
{
    private static string[] NumberedLines(int count) =>
        Enumerable.Range(1, count).Select(n => $"line-{n:D2}").ToArray();

    private static async Task CommitTwoRegionFileAsync(GitHunksFixture fixture, string name = "tracked.txt")
    {
        var lines = NumberedLines(20);
        await fixture.CommitFileAsync(name, string.Join('\n', lines) + "\n");
        lines[1] = "CHANGED-A";
        lines[17] = "CHANGED-B";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, name), string.Join('\n', lines) + "\n",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StageSelectedHunkMovesOnlyThatHunkIntoTheIndex()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);

        var first = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("tracked.txt") });
        Assert.True(first.IsSuccess, first.Summary);
        Assert.Contains("1/2", first.Summary);
        Assert.Contains("@@ -1", first.Content);
        var staged = await fixture.GitAsync("diff", "--staged", "--", "tracked.txt");
        Assert.Contains("+CHANGED-A", staged);
        Assert.DoesNotContain("CHANGED-B", staged);
        Assert.Contains("+CHANGED-B", await fixture.GitAsync("diff", "--", "tracked.txt"));

        var second = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("tracked.txt") });
        Assert.True(second.IsSuccess, second.Summary);
        staged = await fixture.GitAsync("diff", "--staged", "--", "tracked.txt");
        Assert.Contains("+CHANGED-A", staged);
        Assert.Contains("+CHANGED-B", staged);
        Assert.Empty(await fixture.GitAsync("diff", "--", "tracked.txt"));
        var worktree = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "tracked.txt"),
            TestContext.Current.CancellationToken);
        Assert.Contains("CHANGED-A", worktree);
        Assert.Contains("CHANGED-B", worktree);
        Assert.Equal(2, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task WholeRepoFileHashBindsStageHunksToThePreviewedDiff()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        await CommitTwoRegionFileAsync(fixture, "other.txt");

        var result = await fixture.ExecuteAsync("git.stage_hunks", new
        {
            path = "tracked.txt", hunks = new[] { 0 },
            expectedDiff = await fixture.FullDiffFileHashAsync("tracked.txt")
        });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("1/2", result.Summary);
        var staged = await fixture.GitAsync("diff", "--staged", "--", "tracked.txt");
        Assert.Contains("+CHANGED-A", staged);
        Assert.DoesNotContain("CHANGED-B", staged);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--", "other.txt"));
    }

    [Fact]
    public async Task TamperedExpectedDiffIsRefusedAndIndexIsUntouched()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        var expected = await fixture.ExpectedDiffAsync("tracked.txt");
        var tampered = (expected[0] == '0' ? '1' : '0') + expected[1..];

        var result = await fixture.ExecuteAsync("git.stage_hunks",
            new { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = tampered });
        Assert.Equal("git_diff_changed", result.ErrorCode);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
        Assert.Equal(1, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task WorkingTreeDriftAfterDiffReviewIsRefused()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        var expected = await fixture.ExpectedDiffAsync("tracked.txt");
        var lines = await File.ReadAllLinesAsync(Path.Combine(fixture.Root, "tracked.txt"),
            TestContext.Current.CancellationToken);
        lines[9] = "DRIFTED";
        await File.WriteAllLinesAsync(Path.Combine(fixture.Root, "tracked.txt"), lines,
            TestContext.Current.CancellationToken);

        var result = await fixture.ExecuteAsync("git.stage_hunks",
            new { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = expected });
        Assert.Equal("git_diff_changed", result.ErrorCode);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
        Assert.Contains("+DRIFTED", await fixture.GitAsync("diff", "--", "tracked.txt"));
    }

    [Fact]
    public async Task StagedDriftAfterDiffReviewIsRefusedForUnstage()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        await fixture.GitAsync("add", "--", "tracked.txt");
        var expected = await fixture.ExpectedDiffAsync("tracked.txt", staged: true);
        var lines = NumberedLines(20);
        lines[4] = "STAGED-DRIFT";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "tracked.txt"), string.Join('\n', lines) + "\n",
            TestContext.Current.CancellationToken);
        await fixture.GitAsync("add", "--", "tracked.txt");
        var drifted = await fixture.GitAsync("diff", "--staged", "--", "tracked.txt");
        Assert.Contains("+STAGED-DRIFT", drifted);

        var result = await fixture.ExecuteAsync("git.unstage_hunks",
            new { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = expected });
        Assert.Equal("git_diff_changed", result.ErrorCode);
        Assert.Equal(drifted, await fixture.GitAsync("diff", "--staged", "--", "tracked.txt"));
    }

    [Fact]
    public async Task UnstageSelectedHunkLeavesOtherHunksStagedAndWorktreeUntouched()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        await fixture.GitAsync("add", "--", "tracked.txt");

        var result = await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("tracked.txt", staged: true) });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("1/2", result.Summary);
        var staged = await fixture.GitAsync("diff", "--staged", "--", "tracked.txt");
        Assert.Contains("+CHANGED-B", staged);
        Assert.DoesNotContain("CHANGED-A", staged);
        Assert.Contains("+CHANGED-A", await fixture.GitAsync("diff", "--", "tracked.txt"));
        var worktree = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "tracked.txt"),
            TestContext.Current.CancellationToken);
        Assert.Contains("CHANGED-A", worktree);
        Assert.Contains("CHANGED-B", worktree);
        Assert.Equal(1, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task UnmodifiedAndUntrackedFilesHaveNoSelectableHunks()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await fixture.CommitFileAsync("tracked.txt", "baseline\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "new.txt"), "untracked\n",
            TestContext.Current.CancellationToken);
        var empty = GitDiffBinding.Hash(string.Empty);
        var clean = await fixture.ExecuteAsync("git.stage_hunks",
            new { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = empty });
        Assert.Equal("git_hunks_none", clean.ErrorCode);
        var untracked = await fixture.ExecuteAsync("git.stage_hunks",
            new { path = "new.txt", hunks = new[] { 0 }, expectedDiff = empty });
        Assert.Equal("git_hunks_none", untracked.ErrorCode);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
    }

    [Fact]
    public async Task StaleExpectedDiffOnClearedDiffReportsDiffChanged()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        var expected = await fixture.ExpectedDiffAsync("tracked.txt");
        await fixture.GitAsync("checkout", "--", "tracked.txt");

        var result = await fixture.ExecuteAsync("git.stage_hunks",
            new { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = expected });
        Assert.Equal("git_diff_changed", result.ErrorCode);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
    }

    [Fact]
    public async Task BinaryModificationIsRefusedAndIndexIsUntouched()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var binary = Path.Combine(fixture.Root, "blob.bin");
        await File.WriteAllBytesAsync(binary, [0, 1, 2, 3, 4, 5], token);
        await fixture.GitAsync("add", "--", "blob.bin");
        await fixture.GitAsync("commit", "-qm", "binary");
        await File.WriteAllBytesAsync(binary, [0, 9, 2, 3, 4, 5], token);
        var result = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "blob.bin", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("blob.bin") });
        Assert.Equal("git_hunks_unsupported", result.ErrorCode);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
    }

    [Fact]
    public async Task StagedRenameAdditionDeletionAndModeChangeAreRefused()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        await fixture.CommitFileAsync("tracked.txt", "one\ntwo\nthree\n");
        await fixture.CommitFileAsync("old.txt", "a\nb\nc\n");
        await fixture.CommitFileAsync("mode.sh", "echo hi\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "added.txt"), "new\n", token);
        await fixture.GitAsync("add", "--", "added.txt");
        File.Move(Path.Combine(fixture.Root, "old.txt"), Path.Combine(fixture.Root, "renamed.txt"));
        await fixture.GitAsync("add", "-A", "--", "old.txt", "renamed.txt");
        File.Delete(Path.Combine(fixture.Root, "tracked.txt"));
        await fixture.GitAsync("add", "--", "tracked.txt");
        await fixture.GitAsync("update-index", "--chmod=+x", "mode.sh");

        Assert.Equal("git_hunks_unsupported", (await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "added.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("added.txt", staged: true) })).ErrorCode);
        Assert.Equal("git_hunks_unsupported", (await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "renamed.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("renamed.txt", staged: true) })).ErrorCode);
        Assert.Equal("git_hunks_unsupported", (await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "tracked.txt", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("tracked.txt", staged: true) })).ErrorCode);
        Assert.Equal("git_hunks_unsupported", (await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "mode.sh", hunks = new[] { 0 }, expectedDiff = await fixture.ExpectedDiffAsync("mode.sh", staged: true) })).ErrorCode);
        Assert.Contains("A\tadded.txt", await fixture.GitAsync("diff", "--staged", "--name-status"));
        Assert.Contains("R100\told.txt\trenamed.txt", await fixture.GitAsync("diff", "--staged", "--name-status"));
        Assert.Contains("D\ttracked.txt", await fixture.GitAsync("diff", "--staged", "--name-status"));
    }

    [Fact]
    public async Task UnmergedPathsAreRefusedInBothDirections()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await fixture.CommitFileAsync("c.txt", "base\n");
        await fixture.GitAsync("checkout", "-qb", "topic");
        await fixture.CommitFileAsync("c.txt", "topic\n");
        await fixture.GitAsync("checkout", "-q", "-");
        await fixture.CommitFileAsync("c.txt", "main\n");
        var merge = await fixture.GitRawAsync("merge", "--no-ff", "topic");
        Assert.False(merge.IsSuccess);

        var staged = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "c.txt", hunks = new[] { 0 }, expectedDiff = await fixture.DiffShaAsync("c.txt") });
        Assert.Equal("git_hunks_unsupported", staged.ErrorCode);
        var unstaged = await fixture.ExecuteAsync("git.unstage_hunks", new
        { path = "c.txt", hunks = new[] { 0 }, expectedDiff = await fixture.DiffShaAsync("c.txt", staged: true) });
        Assert.Equal("git_hunks_unsupported", unstaged.ErrorCode);
        Assert.Contains("UU c.txt", await fixture.GitAsync("status", "--porcelain"));
    }

    [Fact]
    public async Task OutOfRangeOrdinalReportsTheAvailableHunks()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        await CommitTwoRegionFileAsync(fixture);
        var result = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "tracked.txt", hunks = new[] { 5 }, expectedDiff = await fixture.ExpectedDiffAsync("tracked.txt") });
        Assert.Equal("git_hunks_out_of_range", result.ErrorCode);
        Assert.Contains("0", result.Summary);
        Assert.Empty(await fixture.GitAsync("diff", "--staged", "--name-only"));
    }

    [Fact]
    public async Task NestedLiteralPathIsResolvedAgainstTheRepositoryRoot()
    {
        using var fixture = new GitHunksFixture();
        await fixture.InitializeAsync();
        var token = TestContext.Current.CancellationToken;
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Root, "子目录")).FullName;
        var lines = NumberedLines(20);
        await File.WriteAllTextAsync(Path.Combine(nested, "f.txt"), string.Join('\n', lines) + "\n", token);
        await fixture.GitAsync("add", "--", "子目录/f.txt");
        await fixture.GitAsync("commit", "-qm", "nested");
        lines[1] = "CHANGED-A";
        lines[17] = "CHANGED-B";
        await File.WriteAllTextAsync(Path.Combine(nested, "f.txt"), string.Join('\n', lines) + "\n", token);

        var result = await fixture.ExecuteAsync("git.stage_hunks", new
        { path = "子目录/f.txt", hunks = new[] { 1 }, expectedDiff = await fixture.ExpectedDiffAsync("子目录/f.txt") });
        Assert.True(result.IsSuccess, result.Summary);
        var staged = await fixture.GitAsync("diff", "--staged", "--", "子目录/f.txt");
        Assert.Contains("+CHANGED-B", staged);
        Assert.DoesNotContain("CHANGED-A", staged);
    }
}
