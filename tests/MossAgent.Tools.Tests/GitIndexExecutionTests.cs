using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitIndexExecutionTests
{
    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task RepositoryAuthorizationTimeoutIsSanitizedAndAudited(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        fixture.Repository.Authorize = (_, _) => throw new TimeoutException("secret-token");
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.DoesNotContain("secret-token", result.Summary);
        Assert.Empty(fixture.Launcher.Requests);
        Assert.Equal("tool_timeout", fixture.Audit.Completion?.ErrorCode);
    }

    [Theory]
    [InlineData("git.stage_file", false)]
    [InlineData("git.stage_file", true)]
    [InlineData("git.unstage_file", false)]
    [InlineData("git.unstage_file", true)]
    public async Task CancellationAndTimeoutWaitForOwnedProcessCleanup(string tool, bool timeout)
    {
        using var fixture = new GitIndexExecutionFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Process.Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        fixture.Process.Terminate = async () => { cleaning.SetResult(); await release.Task; };
        var execution = fixture.ExecuteAsync(tool, cancellation.Token);
        try
        {
            Assert.Single(fixture.Launcher.Requests);
            if (timeout) fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
            Assert.False(fixture.Process.Disposed);
            Assert.Null(fixture.Audit.Completion);
        }
        finally { release.TrySetResult(); }
        var result = await execution;
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.True(fixture.Process.Disposed);
        Assert.Equal(1, fixture.Process.Terminations);
        Assert.Equal(result.ErrorCode, fixture.Audit.Completion?.ErrorCode);
    }

    [Theory]
    [InlineData("git.stage_file", false)]
    [InlineData("git.stage_file", true)]
    [InlineData("git.unstage_file", false)]
    [InlineData("git.unstage_file", true)]
    public async Task InterruptedRepositoryAuthorizationCannotLaunchIndexMutation(string tool, bool timeout)
    {
        using var fixture = new GitIndexExecutionFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Authorize = async (_, token) =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { cleaning.SetResult(); await release.Task; }
            throw new InvalidOperationException("Unreachable authorization result.");
        };
        var execution = fixture.ExecuteAsync(tool, cancellation.Token);
        try
        {
            if (timeout) fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", (await execution).ErrorCode);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task ApprovalTimeWorkspaceRevocationPreventsAuthorizationAndLaunch(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        var revoked = false;
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => revoked
            ? Task.FromException(new UnauthorizedAccessException("secret")) : Task.CompletedTask };
        fixture.OnApproval = () => revoked = true;
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken, fixture.Context with { WorkspaceGuard = guard });
        Assert.Equal("workspace_denied", result.ErrorCode);
        Assert.Equal(2, guard.Checks);
        Assert.Equal(1, fixture.Approvals);
        Assert.Equal(0, fixture.Repository.Calls);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_file")]
    [InlineData("git.unstage_file")]
    public async Task BothStreamsAreBoundedAndArgumentsPinLiteralRepositoryFiles(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        fixture.Process.StandardOutput = new StringReader(new string('x', 20000));
        fixture.Process.StandardError = new StringReader(new string('y', 20000));
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken,
            fixture.Context with { MaximumOutputCharacters = 256 }, new { path = "[1].txt", originalPath = "old name.txt" });
        Assert.True(result.IsSuccess);
        Assert.InRange(result.OutputCharacterCount, 1, 256);
        var request = Assert.Single(fixture.Launcher.Requests);
        Assert.Equal(fixture.Root, request.WorkingDirectory);
        Assert.Equal(new[] { "--git-dir=" + Path.Combine(fixture.Root, ".git"), "--work-tree=" + fixture.Root, "--literal-pathspecs" }, request.Arguments.Take(3));
        Assert.Equal(new[] { "--", "[1].txt", "old name.txt" }, request.Arguments.TakeLast(3));
        Assert.True(fixture.Process.Disposed);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }
}
