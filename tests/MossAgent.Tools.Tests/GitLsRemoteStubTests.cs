using System.Text.Json;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitLsRemoteStubTests
{
    [Fact]
    public async Task ProbeSendsBoundedNonInteractiveArgumentsAndSanitizesTheEndpoint()
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.LsRemoteOutput =
            new string('a', 40) + "\trefs/heads/main\n" + new string('b', 40) + "\trefs/tags/v1\n";
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.True(result.IsSuccess, result.Summary);
        var report = JsonSerializer.Deserialize<GitLsRemoteReport>(result.Content!, JsonSerializerOptions.Web)!;
        Assert.Equal("https://example.invalid", report.Endpoint);
        Assert.DoesNotContain("private-probe", result.ToString());
        var request = Assert.Single(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
        Assert.Contains("protocol.allow=never", request.Arguments);
        Assert.Contains("protocol.https.allow=always", request.Arguments);
        Assert.Contains("core.askPass=", request.Arguments);
        Assert.DoesNotContain(request.Arguments, value => value.Contains("sshCommand"));
        Assert.Equal("0", request.Environment?["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("never", request.Environment?["GCM_INTERACTIVE"]);
        Assert.Null(request.RemovedEnvironmentPrefixes);
    }

    [Fact]
    public async Task SshTransportAddsBatchModeSshCommandWithoutTrustingHostKeys()
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.Configuration = "remote.origin.url\nssh://git@example.invalid/project\0";
        fixture.Processes.RemoteUrl = "ssh://git@example.invalid/project";
        fixture.Processes.LsRemoteOutput = new string('a', 40) + "\trefs/heads/main\n";
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.True(result.IsSuccess, result.Summary);
        var request = Assert.Single(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
        Assert.Contains("protocol.ssh.allow=always", request.Arguments);
        Assert.Contains("core.sshCommand=ssh -o BatchMode=yes -o ConnectTimeout=10", request.Arguments);
        Assert.DoesNotContain(request.Arguments, value => value.Contains("StrictHostKeyChecking"));
    }

    [Theory]
    [InlineData("main", "refs/heads/main")]
    [InlineData("feature/topic", "refs/heads/feature/topic")]
    [InlineData("refs/tags/v1", "refs/tags/v1")]
    [InlineData("HEAD", "HEAD")]
    public async Task RefFilterIsNormalizedIntoASinglePatternArgument(string value, string pattern)
    {
        using var fixture = new GitLsRemoteStubFixture();
        await fixture.ExecuteAsync(new { remote = "origin", @ref = value });
        var request = Assert.Single(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
        Assert.Equal(pattern, request.Arguments[^1]);
    }

    [Fact]
    public async Task RefusalFailsClosedWithoutEchoingCredentialsOrRawOutput()
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.LsRemoteExitCode = 128;
        fixture.Processes.LsRemoteError =
            "fatal: unable to access 'https://user:private-probe@example.invalid/project': Could not resolve host\n";
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_failed", result.ErrorCode);
        Assert.DoesNotContain("private-probe", result.ToString());
        Assert.DoesNotContain("example.invalid", result.ToString());
        Assert.Equal(result.ErrorCode, fixture.Audit.Completion?.ErrorCode);
    }

    [Theory]
    [InlineData("zzz\trefs/heads/main\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\tnot-a-ref\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa refs/heads/main\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\trefs/heads/main\ngarbage\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\trefs/heads/main")]
    public async Task MalformedOrIncompleteOutputFailsClosed(string stdout)
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.LsRemoteOutput = stdout;
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_incomplete", result.ErrorCode);
        Assert.DoesNotContain("refs/heads/main", result.ToString());
    }

    [Fact]
    public async Task MoreThanSixtyFourRefsAndOversizedStreamsReportTruncated()
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.LsRemoteOutput = string.Concat(Enumerable.Range(0, 70).Select(index =>
            new string('a', 40) + $"\trefs/heads/branch{index:00}\n"));
        var report = JsonSerializer.Deserialize<GitLsRemoteReport>(
            (await fixture.ExecuteAsync(new { remote = "origin" })).Content!, JsonSerializerOptions.Web)!;
        Assert.True(report.Truncated);
        Assert.Equal(64, report.Refs.Count);
        fixture.Processes.LsRemoteOutput = string.Concat(Enumerable.Range(0, 20000).Select(index =>
            new string('a', 40) + $"\trefs/heads/branch{index:00000}\n"));
        var truncated = JsonSerializer.Deserialize<GitLsRemoteReport>(
            (await fixture.ExecuteAsync(new { remote = "origin" })).Content!, JsonSerializerOptions.Web)!;
        Assert.True(truncated.Truncated);
        Assert.Equal(64, truncated.Refs.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndTimeoutWaitForActualProbeCleanupAndAudit(bool timeout)
    {
        using var fixture = new GitLsRemoteStubFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Processes.LsRemoteWait = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        fixture.Processes.LsRemoteTerminate = async () => { cleaning.TrySetResult(); await release.Task; };
        var execution = fixture.ExecuteAsync(new { remote = "origin" }, cancellation.Token);
        try
        {
            if (timeout) fixture.Clock.Advance(TimeSpan.FromSeconds(5));
            else await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(execution.IsCompleted);
            Assert.False(Assert.Single(fixture.Processes.LsRemoteProcesses).Disposed);
        }
        finally { release.TrySetResult(); }
        var result = await execution;
        Assert.Equal(timeout ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        var process = Assert.Single(fixture.Processes.LsRemoteProcesses);
        Assert.True(process.Disposed);
        Assert.Equal(1, process.Terminations);
        Assert.Equal(result.ErrorCode, fixture.Audit.Completion?.ErrorCode);
    }

    [Fact]
    public async Task AmbientGitVariablesAreStrippedWhileSuppliedOnesSurvive()
    {
        Environment.SetEnvironmentVariable("GIT_LS_REMOTE_PROBE", "ambient");
        try
        {
            using var fixture = new GitLsRemoteStubFixture();
            await fixture.ExecuteAsync(new { remote = "origin" });
            var request = Assert.Single(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
            Assert.True(request.Environment!.TryGetValue("GIT_LS_REMOTE_PROBE", out var ambient) && ambient is null);
            Assert.Equal("0", request.Environment["GIT_TERMINAL_PROMPT"]);
        }
        finally { Environment.SetEnvironmentVariable("GIT_LS_REMOTE_PROBE", null); }
    }

    [Theory]
    [InlineData("remote.origin.url\nftp://example.invalid/repo\0", "ftp://example.invalid/repo")]
    [InlineData("remote.origin.url\nhttps://example.invalid/repo\0remote.origin.vcs\ncustom\0", "https://example.invalid/repo")]
    public async Task UnsupportedTransportsAreRejectedBeforeProbing(string configuration, string url)
    {
        using var fixture = new GitLsRemoteStubFixture();
        fixture.Processes.Configuration = configuration;
        fixture.Processes.RemoteUrl = url;
        var result = await fixture.ExecuteAsync(new { remote = "origin" });
        Assert.Equal("git_remote_probe_transport", result.ErrorCode);
        Assert.DoesNotContain(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
    }

    [Fact]
    public async Task MissingRemoteFailsWithFixedCodeBeforeProbing()
    {
        using var fixture = new GitLsRemoteStubFixture();
        var result = await fixture.ExecuteAsync(new { remote = "ghost" });
        Assert.Equal("git_remote_missing", result.ErrorCode);
        Assert.DoesNotContain(fixture.Processes.Requests, entry => entry.Arguments.Contains("ls-remote"));
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("refs/../x")]
    [InlineData("@{0}")]
    [InlineData("refs/")]
    public async Task InvalidRefShapesAreRejectedWithoutLaunchingAnyProcess(string value)
    {
        using var fixture = new GitLsRemoteStubFixture();
        var result = await fixture.ExecuteAsync(new { remote = "origin", @ref = value });
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Empty(fixture.Processes.Requests);
    }
}
