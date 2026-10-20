using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitCommitExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndTimeoutAwaitCommitProcessCleanupAndAudit(bool timeout)
    {
        using var fixture = new GitCommitExecutionFixture();
        var state = await fixture.PreviewAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Processes.Commit.Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        fixture.Processes.Commit.Terminate = async () => { cleaning.SetResult(); await release.Task; };
        var execution = fixture.ExecuteAsync("git.commit", new { message = "literal --all", expectedState = state }, cancellation.Token);
        try
        {
            Assert.Single(fixture.Processes.Requests, request => request.Arguments.Contains("commit"));
            if (timeout) fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
            Assert.False(fixture.Processes.Commit.Disposed);
        }
        finally { release.TrySetResult(); }
        var result = await execution;
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.True(fixture.Processes.Commit.Disposed);
        Assert.Equal(result.ErrorCode, fixture.Audit.Completion?.ErrorCode);
    }

    [Fact]
    public async Task OversizedIndexAndRepositoryTimeoutFailClosedWithoutCommit()
    {
        using var fixture = new GitCommitExecutionFixture();
        fixture.Processes.Index = new string('x', 8 * 1024 * 1024);
        var result = await fixture.ExecuteAsync("git.commit_preview", new { }, TestContext.Current.CancellationToken);
        Assert.Equal("git_preview_incomplete", result.ErrorCode);
        fixture.Repository.Authorize = (_, _) => throw new TimeoutException("synthetic-private-value");
        result = await fixture.ExecuteAsync("git.commit", new { message = "not run", expectedState = new string('0', 64) }, TestContext.Current.CancellationToken);
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.DoesNotContain("synthetic-private-value", result.Summary);
        Assert.DoesNotContain(fixture.Processes.Requests, request => request.Arguments.Contains("commit"));
    }

    [Fact]
    public async Task CommitArgumentsCannotStageAmendPushOrOverrideHookAndSigningConfiguration()
    {
        using var fixture = new GitCommitExecutionFixture();
        var state = await fixture.PreviewAsync();
        fixture.Processes.Commit.StandardOutput = new StringReader(new string('x', 20000));
        fixture.Processes.Commit.StandardError = new StringReader(new string('y', 20000));
        var result = await fixture.ExecuteAsync("git.commit", new { message = "--all\n--amend\n--no-verify", expectedState = state },
            TestContext.Current.CancellationToken, fixture.Context with { MaximumOutputCharacters = 256 });
        Assert.True(result.IsSuccess);
        Assert.InRange(result.OutputCharacterCount, 1, 256);
        var request = Assert.Single(fixture.Processes.Requests, request => request.Arguments.Contains("commit"));
        Assert.Equal(new[] { "commit", "--cleanup=verbatim", "--message=--all\n--amend\n--no-verify" }, request.Arguments.TakeLast(3));
        Assert.Equal(new[] { "GIT_" }, request.RemovedEnvironmentPrefixes);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task GuardRevokedDuringApprovalPreventsEveryCommitQueryAndMutation()
    {
        using var fixture = new GitCommitExecutionFixture();
        var state = await fixture.PreviewAsync();
        fixture.Processes.Requests.Clear();
        var revoked = false;
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => revoked
            ? Task.FromException(new UnauthorizedAccessException("not disclosed")) : Task.CompletedTask };
        fixture.OnApproval = () => revoked = true;
        var result = await fixture.ExecuteAsync("git.commit", new { message = "not run", expectedState = state },
            TestContext.Current.CancellationToken, fixture.Context with { WorkspaceGuard = guard });
        Assert.Equal("workspace_denied", result.ErrorCode);
        Assert.Equal(2, guard.Checks);
        Assert.Empty(fixture.Processes.Requests);
    }
}
