using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitSynchronizationContextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BranchLookup_DoesNotNeedTheCallersMessagePump(bool detached)
    {
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new StubTaskProcess
        {
            ExitCode = detached ? 1 : 0,
            StandardOutput = new StringReader(detached ? string.Empty : "main\n"),
            Wait = token => exit.Task.WaitAsync(token)
        };
        var fallback = new StubTaskProcess { StandardOutput = new StringReader("detached\n") };
        var launches = 0;
        var launcher = new RecordingTaskProcessLauncher(() => ++launches == 1 ? first : fallback);
        var service = new GitRepositoryInfoService(new GitProcessRunner(launcher));
        var context = new QueuedSynchronizationContext();
        var original = SynchronizationContext.Current;
        Task<string?> execution;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            execution = service.GetBranchNameAsync(Path.GetTempPath(), TestContext.Current.CancellationToken);
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        exit.SetResult();
        try
        {
            Assert.Equal(detached ? "detached" : "main",
                await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(0, context.Posts);
            Assert.True(first.Disposed);
            if (detached) Assert.True(fallback.Disposed);
        }
        finally { context.Drain(); }
    }

    [Fact]
    public async Task Cancellation_DrainsCleanupWithoutTheCallersMessagePump()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new StubTaskProcess { Wait = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var runner = new GitProcessRunner(new RecordingTaskProcessLauncher(() => process));
        var context = new QueuedSynchronizationContext();
        var original = SynchronizationContext.Current;
        Task<GitProcessResult> execution;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            execution = runner.RunAsync(Path.GetTempPath(), ["status"], cancellation.Token);
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        cancellation.Cancel();
        try
        {
            var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(cancellation.Token, failure.CancellationToken);
            Assert.Equal(0, context.Posts);
            Assert.Equal(1, process.Terminations);
            Assert.True(process.Disposed);
        }
        finally { context.Drain(); }
    }
}
