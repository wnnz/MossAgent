using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitPushExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndTimeoutWaitForActualPushCleanupAndAudit(bool timeout)
    {
        using var fixture = new GitPushExecutionFixture();
        var preview = await fixture.PreviewAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Processes.Push.Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        fixture.Processes.Push.Terminate = async () => { cleaning.TrySetResult(); await release.Task; };
        var execution = fixture.ExecuteAsync("git.push", new { remote = preview.Remote, targetBranch = preview.TargetBranch, expectedState = preview.State }, cancellation.Token);
        try
        {
            if (timeout) fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
            Assert.False(fixture.Processes.Push.Disposed);
        }
        finally { release.TrySetResult(); }
        var result = await execution;
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.True(fixture.Processes.Push.Disposed);
        Assert.Equal(result.ErrorCode, fixture.Audit.Completion?.ErrorCode);
    }

    [Fact]
    public async Task ArgumentsPinTheCommitAndDisableBroadDefaultsWithoutExposingOutput()
    {
        using var fixture = new GitPushExecutionFixture();
        var preview = await fixture.PreviewAsync();
        fixture.Processes.Push.StandardOutput = new StringReader(new string('x', 20000) + "private-probe");
        fixture.Processes.Push.StandardError = new StringReader("private-stderr");
        var result = await fixture.ExecuteAsync("git.push", new { remote = preview.Remote, targetBranch = preview.TargetBranch, expectedState = preview.State }, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("private", result.ToString());
        Assert.Null(result.Content);
        var request = Assert.Single(fixture.Processes.Requests, request => request.Arguments.Contains("push"));
        Assert.Equal(new[] { "--", "origin", new string('a', 40) + ":refs/heads/fixture" }, request.Arguments.TakeLast(3));
        foreach (var option in new[] { "--no-force", "--no-mirror", "--no-prune", "--no-follow-tags", "--recurse-submodules=no", "remote.origin.mirror=false", "push.autoSetupRemote=false" })
            Assert.Contains(option, request.Arguments);
        Assert.DoesNotContain("--no-verify", request.Arguments);
        Assert.DoesNotContain("--set-upstream", request.Arguments);
        Assert.Equal(new[] { "GIT_" }, request.RemovedEnvironmentPrefixes);
    }

    [Fact]
    public async Task OversizedConfigurationAndRevokedGuardFailBeforePush()
    {
        using var fixture = new GitPushExecutionFixture();
        var preview = await fixture.PreviewAsync();
        fixture.Processes.Configuration = new string('x', 1024 * 1024);
        var result = await fixture.ExecuteAsync("git.push_preview", new { }, TestContext.Current.CancellationToken);
        Assert.Equal("git_push_preview_incomplete", result.ErrorCode);
        fixture.Processes.Requests.Clear();
        var revoked = false;
        var guard = new WorkspaceGuardProbe { Handler = (_, _) => revoked ? Task.FromException(new UnauthorizedAccessException("private-guard")) : Task.CompletedTask };
        fixture.OnApproval = () => revoked = true;
        result = await fixture.ExecuteAsync("git.push", new { remote = preview.Remote, targetBranch = preview.TargetBranch, expectedState = preview.State },
            TestContext.Current.CancellationToken, fixture.Context with { WorkspaceGuard = guard });
        Assert.Equal("workspace_denied", result.ErrorCode);
        Assert.Empty(fixture.Processes.Requests);
    }
}
