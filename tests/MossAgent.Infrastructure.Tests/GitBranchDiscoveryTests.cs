using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitBranchDiscoveryTests
{
    [Fact]
    public async Task SymbolicBranchWithoutACommit_DoesNotRequireAnExistingHead()
    {
        var process = new StubTaskProcess { StandardOutput = new StringReader("unborn-main\n") };
        var launcher = new RecordingTaskProcessLauncher(() => process);

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Equal("unborn-main", branch);
        Assert.Equal(["symbolic-ref", "--quiet", "--short", "HEAD"], Assert.Single(launcher.Requests).Arguments);
        Assert.True(process.Disposed);
    }

    [Theory]
    [InlineData(0, "HEAD\n", "HEAD")]
    [InlineData(128, "HEAD\n", null)]
    public async Task DetachedHead_RetainsTheBoundedFallback(int exit, string output, string? expected)
    {
        var symbolic = new StubTaskProcess { ExitCode = 1 };
        var fallback = new StubTaskProcess { ExitCode = exit, StandardOutput = new StringReader(output) };
        var queue = new Queue<StubTaskProcess>([symbolic, fallback]);
        var launcher = new RecordingTaskProcessLauncher(queue.Dequeue);

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Equal(expected, branch);
        Assert.Equal(2, launcher.Requests.Count);
        Assert.Equal(["rev-parse", "--abbrev-ref", "HEAD"], launcher.Requests[1].Arguments);
        Assert.All(launcher.Requests, request => Assert.Equal(Path.GetTempPath(), request.WorkingDirectory));
        Assert.True(symbolic.Disposed);
        Assert.True(fallback.Disposed);
    }

    [Fact]
    public async Task CancellationAfterSymbolicQuery_DoesNotStartDetachedFallback()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { ExitCode = 1, Terminate = () => { cancellation.Cancel(); return Task.CompletedTask; } };
        var launcher = new RecordingTaskProcessLauncher(() => process);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitRepositoryInfoService(new GitProcessRunner(launcher)).GetBranchNameAsync(Path.GetTempPath(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Single(launcher.Requests);
        Assert.True(process.Disposed);
    }

    [Fact]
    public async Task TruncatedSymbolicFailure_DoesNotLaunchFallbackOrReturnPartialName()
    {
        var process = new StubTaskProcess { ExitCode = 1, StandardOutput = new StringReader(new string('x', 50000)) };
        var launcher = new RecordingTaskProcessLauncher(() => process);

        var branch = await new GitRepositoryInfoService(new GitProcessRunner(launcher))
            .GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);

        Assert.Null(branch);
        Assert.Single(launcher.Requests);
        Assert.True(process.Disposed);
    }
}
