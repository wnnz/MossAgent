using System.ComponentModel;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitCommandRunnerTests
{
    [Theory]
    [InlineData(0, "stdout", "stderr", false, true)]
    [InlineData(1, "patch", "", false, false)]
    [InlineData(1, "patch", "", true, true)]
    [InlineData(1, "patch", "error", true, false)]
    [InlineData(2, "patch", "", true, false)]
    public async Task OwnedLaunchPreservesArgumentsOutputAndDiffExitRules(int exitCode, string stdout, string stderr,
        bool acceptDiff, bool success)
    {
        var process = new StubTaskProcess
        {
            ExitCode = exitCode,
            StandardOutput = new StringReader(stdout),
            StandardError = new StringReader(stderr)
        };
        var launcher = new RecordingTaskProcessLauncher(() => process);
        var context = Context();
        string[] arguments = ["diff", "--", "文件 with 'quotes'.cs"];

        var result = await new GitCommandRunner(launcher).RunAsync(context.WorkingDirectory, arguments,
            context, TestContext.Current.CancellationToken, acceptDiff);

        var request = Assert.Single(launcher.Requests);
        Assert.Equal("git", request.FileName);
        Assert.Equal(context.WorkingDirectory, request.WorkingDirectory);
        Assert.Equal(arguments, request.Arguments);
        Assert.False(request.RedirectStandardInput);
        Assert.Equal(success, result.IsSuccess);
        if (success) Assert.Contains(stdout, result.Content);
        else Assert.Equal("git_failed", result.ErrorCode);
        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task PreCancelledCallDoesNotLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitCommandRunner(launcher).RunAsync(Context().WorkingDirectory, ["status"], Context(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Empty(launcher.Requests);
    }

    [Fact]
    public async Task CancellationTerminatesAndDisposesBeforeReturning()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var launcher = new RecordingTaskProcessLauncher(() => process);
        var execution = new GitCommandRunner(launcher).RunAsync(Context().WorkingDirectory, ["status"], Context(), cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReaderFailuresAreObservedAfterProcessCleanup(bool stderr)
    {
        var process = new StubTaskProcess();
        (stderr ? process.StandardError : process.StandardOutput).Dispose();
        var launcher = new RecordingTaskProcessLauncher(() => process);

        await Assert.ThrowsAsync<ObjectDisposedException>(() => new GitCommandRunner(launcher)
            .RunAsync(Context().WorkingDirectory, ["status"], Context(), TestContext.Current.CancellationToken));

        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task CancellationDuringCleanupCannotReturnSuccess()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { Terminate = () => { cancellation.Cancel(); return Task.CompletedTask; } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitCommandRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Context().WorkingDirectory, ["status"], Context(), cancellation.Token));

        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task TerminationFailureStillDisposesAndIsNotReportedAsSuccess()
    {
        var process = new StubTaskProcess { Terminate = () => Task.FromException(new IOException("synthetic cleanup failure")) };

        await Assert.ThrowsAsync<IOException>(() => new GitCommandRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Context().WorkingDirectory, ["status"], Context(), TestContext.Current.CancellationToken));

        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task LaunchFailureIsSanitizedByThePipelineAndNeverFallsBack()
    {
        var launcher = new RecordingTaskProcessLauncher(() => throw new Win32Exception("do-not-disclose"));
        var executor = Executor(launcher);

        var result = await executor.ExecuteAsync(Request("git.status", new { }), Context(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("do-not-disclose", result.Summary);
        Assert.Single(launcher.Requests);
    }

    [Fact]
    public async Task PipelineTimeoutCleansUpTheOwnedProcess()
    {
        var process = new StubTaskProcess { Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var clock = new ManualToolTimeProvider();
        var execution = Executor(new RecordingTaskProcessLauncher(() => process), clock)
            .ExecuteAsync(Request("git.status", new { }), Context() with { Timeout = TimeSpan.FromSeconds(1) },
                TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(1));
        var result = await execution;

        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(512)]
    public async Task PipelineStillLimitsBothOutputStreams(int limit)
    {
        var process = new StubTaskProcess
        {
            StandardOutput = new StringReader(new string('x', 50000)),
            StandardError = new StringReader(new string('y', 50000))
        };

        var result = await Executor(new RecordingTaskProcessLauncher(() => process)).ExecuteAsync(Request("git.status", new { }),
            Context() with { MaximumOutputCharacters = limit }, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.InRange(result.OutputCharacterCount, 1, limit);
        Assert.True(process.Disposed);
    }

    [Theory]
    [InlineData("git.status")]
    [InlineData("git.diff")]
    [InlineData("git.log")]
    [InlineData("git.create_worktree")]
    public async Task InvalidSchemaNeverReachesTheLauncher(string toolName)
    {
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());

        var result = await Executor(launcher).ExecuteAsync(Request(toolName, new { repository = 42 }), Context(),
            TestContext.Current.CancellationToken);

        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Empty(launcher.Requests);
    }

    private static ToolExecutionContext Context() =>
        new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess);

    private static ToolRequest Request(string toolName, object arguments) =>
        new("git-runner", toolName, JsonSerializer.SerializeToElement(arguments));

    private static ToolExecutor Executor(RecordingTaskProcessLauncher launcher, TimeProvider? clock = null)
    {
        var paths = new AuthorizedPathResolver();
        var runner = new GitCommandRunner(launcher);
        return new(new ToolRegistry([new GitStatusTool(paths, runner), new GitDiffTool(paths, runner), new GitLogTool(paths, runner),
            new GitCreateWorktreeTool(paths, runner)]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)),
            timeProvider: clock);
    }
}
