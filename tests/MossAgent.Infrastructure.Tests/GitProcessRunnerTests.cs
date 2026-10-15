using System.ComponentModel;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitProcessRunnerTests
{
    [Fact]
    public async Task CapturesBothStreamsAndPreservesTheExplicitLaunchRequest()
    {
        var process = new StubTaskProcess
        {
            ExitCode = 7,
            StandardOutput = new StringReader("output"),
            StandardError = new StringReader("error")
        };
        var launcher = new RecordingTaskProcessLauncher(() => process);
        string[] arguments = ["worktree", "add", "directory with spaces", "HEAD"];

        var result = await new GitProcessRunner(launcher).RunAsync(Path.GetTempPath(), arguments, TestContext.Current.CancellationToken);

        var request = Assert.Single(launcher.Requests);
        Assert.Equal("git", request.FileName);
        Assert.Equal(Path.GetTempPath(), request.WorkingDirectory);
        Assert.Equal(arguments, request.Arguments);
        Assert.False(request.RedirectStandardInput);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("output" + Environment.NewLine + "error", result.Output);
        Assert.False(result.OutputTruncated);
        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task StreamBudgetsDoNotStopDrainingAndDoNotFabricateAPartialBranch()
    {
        var process = new StubTaskProcess
        {
            StandardOutput = new StringReader(new string('x', 50000)),
            StandardError = new StringReader(new string('y', 50000))
        };
        var result = await new GitProcessRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Path.GetTempPath(), ["status"], TestContext.Current.CancellationToken);
        Assert.Equal(16384, result.StandardOutput.Length);
        Assert.Equal(16384, result.StandardError.Length);
        Assert.True(result.OutputTruncated);
        Assert.True(process.Disposed);
        var branchProcess = new StubTaskProcess { StandardOutput = new StringReader(new string('x', 50000)) };

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(new RecordingTaskProcessLauncher(() => branchProcess)))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Null(branch);
        Assert.True(branchProcess.Disposed);
    }

    [Theory]
    [InlineData(0, "main\n", "", "main")]
    [InlineData(0, "", "", null)]
    [InlineData(128, "main", "not a repository", null)]
    [InlineData(0, "main\n", "diagnostic", "main")]
    public async Task BranchQueriesRetainTheirSuccessAndEmptySemantics(int exit, string stdout, string stderr, string? expected)
    {
        var process = new StubTaskProcess
        {
            ExitCode = exit,
            StandardOutput = new StringReader(stdout),
            StandardError = new StringReader(stderr)
        };
        var launcher = new RecordingTaskProcessLauncher(() => process);

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, branch);
        Assert.Equal(["symbolic-ref", "--quiet", "--short", "HEAD"], Assert.Single(launcher.Requests).Arguments);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task MissingDirectoryDoesNotLaunchGit()
    {
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), TestContext.Current.CancellationToken);

        Assert.Null(branch);
        Assert.Empty(launcher.Requests);
    }

    [Fact]
    public async Task MissingGitReturnsUnknownBranchWithoutAnUnownedFallback()
    {
        var launcher = new RecordingTaskProcessLauncher(() => throw new Win32Exception("synthetic missing git"));

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Null(branch);
        Assert.Single(launcher.Requests);
    }

    [Fact]
    public async Task PreCancellationDoesNotLaunchAndPreservesTheCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitProcessRunner(launcher)
            .RunAsync(Path.GetTempPath(), ["status"], cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Empty(launcher.Requests);
    }

    [Fact]
    public async Task CancellationDrainsCleanupAndPreservesTheCallerToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var execution = new GitProcessRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Path.GetTempPath(), ["status"], cancellation.Token);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task PreCancelledWorktreeCreationDoesNotCreateApplicationDirectories()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var project = new ProjectProfile(Guid.NewGuid(), "synthetic", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UnixEpoch);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitWorktreeService(new AppDataPaths(root), new GitProcessRunner(launcher))
            .CreateAsync(project, Guid.NewGuid(), cancellation.Token));

        Assert.Empty(launcher.Requests);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task DeadlineIsDistinctFromUserCancellationAndCleansUp()
    {
        var clock = new ManualToolTimeProvider();
        var process = new StubTaskProcess { Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var execution = new GitProcessRunner(new RecordingTaskProcessLauncher(() => process), clock)
            .RunAsync(Path.GetTempPath(), ["status"], TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(30));
        await Assert.ThrowsAsync<TimeoutException>(() => execution);

        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReaderFailureStillTerminatesAndDisposes(bool stderr)
    {
        var process = new StubTaskProcess();
        (stderr ? process.StandardError : process.StandardOutput).Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => new GitProcessRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Path.GetTempPath(), ["status"], TestContext.Current.CancellationToken));

        Assert.Equal(1, process.Terminations);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task CancellationDuringCleanupCannotReturnSuccess()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { Terminate = () => { cancellation.Cancel(); return Task.CompletedTask; } };

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitProcessRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Path.GetTempPath(), ["status"], cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task TerminationFailureDoesNotHideCleanupOrReturnSuccess()
    {
        var process = new StubTaskProcess { Terminate = () => Task.FromException(new IOException("synthetic cleanup failure")) };

        await Assert.ThrowsAsync<IOException>(() => new GitProcessRunner(new RecordingTaskProcessLauncher(() => process))
            .RunAsync(Path.GetTempPath(), ["status"], TestContext.Current.CancellationToken));

        Assert.True(process.Disposed);
    }
}
