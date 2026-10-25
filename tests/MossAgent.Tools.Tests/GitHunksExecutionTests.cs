using MossAgent.Domain;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitHunksExecutionTests
{
    private const string TwoHunkDiff = """
        diff --git a/f.txt b/f.txt
        index 4f11e05..980abbb 100644
        --- a/f.txt
        +++ b/f.txt
        @@ -1,5 +1,5 @@
         a1
        -a2
        +CHANGED
         a3
         a4
         a5
        @@ -7,4 +7,5 @@ a6
         a7
         a8
         a9
        -a10
        +B10
        +B11
        """;

    private static readonly string PlaceholderHash = new('0', 64);

    [Theory]
    [InlineData("git.stage_hunks", false)]
    [InlineData("git.unstage_hunks", true)]
    public async Task SelectedHunkPatchReachesGitApplyThroughStandardInput(string tool, bool reverse)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        var writer = new RecordingTextWriter();
        fixture.Enqueue(TwoHunkDiff);
        fixture.Enqueue(input: writer);
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 1 }, expectedDiff = GitDiffBinding.Hash(TwoHunkDiff) }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(2, fixture.Launcher.Requests.Count);
        var diff = fixture.Launcher.Requests[0];
        Assert.False(diff.RedirectStandardInput);
        Assert.Contains("diff", diff.Arguments);
        Assert.Equal(reverse, diff.Arguments.Contains("--staged"));
        var apply = fixture.Launcher.Requests[1];
        Assert.True(apply.RedirectStandardInput);
        Assert.Contains("apply", apply.Arguments);
        Assert.Contains("--cached", apply.Arguments);
        Assert.Equal(reverse, apply.Arguments.Contains("--reverse"));
        Assert.Equal("-", apply.Arguments[^1]);
        var patch = writer.ToString();
        Assert.Contains("@@ -7,4 +7,5 @@", patch);
        Assert.DoesNotContain("@@ -1,5 +1,5 @@", patch);
        Assert.StartsWith("diff --git a/f.txt b/f.txt\nindex 4f11e05..980abbb 100644\n--- a/f.txt\n+++ b/f.txt\n", patch);
        Assert.True(writer.Disposed);
        Assert.Equal(1, fixture.ApprovalRequests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task DriftedExpectedDiffNeverReachesGitApply(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Enqueue(TwoHunkDiff);
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 1 }, expectedDiff = PlaceholderHash }, token);
        Assert.Equal("git_diff_changed", result.ErrorCode);
        Assert.Single(fixture.Launcher.Requests);
        Assert.Equal(1, fixture.ApprovalRequests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task ApplyFailureSurfacesGitFailedWithoutHanging(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Enqueue(TwoHunkDiff);
        fixture.Enqueue(exitCode: 1, standardError: "error: corrupt patch at line 3\n", input: new RecordingTextWriter());
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = GitDiffBinding.Hash(TwoHunkDiff) }, token);
        Assert.Equal("git_failed", result.ErrorCode);
        Assert.Contains("corrupt patch", result.Summary);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task OutOfRangeOrdinalNeverReachesGitApply(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Enqueue(TwoHunkDiff);
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 2 }, expectedDiff = GitDiffBinding.Hash(TwoHunkDiff) }, token);
        Assert.Equal("git_hunks_out_of_range", result.ErrorCode);
        Assert.Single(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task EmptyDiffReportsNothingSelectable(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Enqueue();
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = GitDiffBinding.Hash(string.Empty) }, token);
        Assert.Equal("git_hunks_none", result.ErrorCode);
        Assert.Single(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task MalformedDiffOutputIsRefused(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        const string malformed = "warning: unexpected output\n";
        fixture.Enqueue(malformed);
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = GitDiffBinding.Hash(malformed) }, token);
        Assert.Equal("git_hunks_unsupported", result.ErrorCode);
        Assert.Single(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks", "../outside.txt")]
    [InlineData("git.stage_hunks", ".git/index")]
    [InlineData("git.unstage_hunks", ".")]
    public async Task InvalidFileScopeIsDeniedBeforeApprovalOrLaunch(string tool, string path)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        var result = await fixture.ExecuteAsync(tool,
            new { path, hunks = new[] { 0 }, expectedDiff = PlaceholderHash }, token);
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task RepositoryAuthorizationTimeoutIsSanitized(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Repository.Authorize = (_, _) => throw new TimeoutException("secret-token");
        var result = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = PlaceholderHash }, token);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.DoesNotContain("secret-token", result.Summary);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task ReadOnlyPolicyAndMalformedArgumentsNeverReachApproval(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        var denied = await fixture.ExecuteAsync(tool,
            new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = PlaceholderHash }, token,
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.Equal("capability_denied", denied.ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = Array.Empty<int>(), expectedDiff = PlaceholderHash }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { 0, 1000 }, expectedDiff = PlaceholderHash }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { "0" }, expectedDiff = PlaceholderHash }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", expectedDiff = PlaceholderHash }, token)).ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_hunks")]
    [InlineData("git.unstage_hunks")]
    public async Task ExpectedDiffIsRequiredAndMustBeALowercaseSha256(string tool)
    {
        using var fixture = new GitHunksStubFixture();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { 0 } }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = PlaceholderHash[..63] }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = new string('A', 64) }, token)).ErrorCode);
        Assert.Equal("invalid_arguments",
            (await fixture.ExecuteAsync(tool, new { path = "f.txt", hunks = new[] { 0 }, expectedDiff = new string('g', 64) }, token)).ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Empty(fixture.Launcher.Requests);
    }
}
