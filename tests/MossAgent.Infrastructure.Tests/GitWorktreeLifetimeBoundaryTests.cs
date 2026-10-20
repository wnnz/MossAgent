using MossAgent.Application.Git;
using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeLifetimeBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationRetainsOwnershipUntilDiscoveryProcessCleanupFinishes(bool removal)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var target = removal ? await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token) : fixture.Target(Guid.NewGuid());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var process = new StubTaskProcess
        {
            Wait = token => { entered.SetResult(); return Task.Delay(Timeout.InfiniteTimeSpan, token); },
            Terminate = () => { cleaning.SetResult(); return release.Task; }
        };
        var launcher = new RecordingTaskProcessLauncher(() => process);
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        var execution = removal ? service.RemoveAsync(fixture.Project, target, cancellation.Token)
            : service.CreateAsync(fixture.Project, Guid.NewGuid(), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await cancellation.CancelAsync();
            await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.False(execution.IsCompleted);
        }
        finally { release.TrySetResult(); }
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(process.Disposed);
        Assert.Single(launcher.Requests);
        if (removal) Assert.True(Directory.Exists(target));
        else Assert.False(Directory.Exists(fixture.Paths.Root));
    }

    [Fact]
    public async Task TotalDeadlineDoesNotRestartForTheMutationCommand()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var clock = new ManualToolTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var metadata = Path.Combine(fixture.Repository, ".git");
        var first = new StubTaskProcess
        {
            StandardOutput = new StringReader($"{fixture.Repository}\n{metadata}\n{metadata}\n{new string('a', 40)}\n"),
            Wait = _ => { clock.Advance(TimeSpan.FromSeconds(20)); return Task.CompletedTask; }
        };
        var second = new StubTaskProcess
        {
            Wait = token => { entered.SetResult(); return Task.Delay(Timeout.InfiniteTimeSpan, token); }
        };
        var responses = new Queue<StubTaskProcess>([first, second]);
        var launcher = new RecordingTaskProcessLauncher(() => responses.Dequeue());
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher), clock);
        var execution = service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<TimeoutException>(() => execution);
        Assert.Equal(2, launcher.Requests.Count);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
        Assert.DoesNotContain(launcher.Requests, request => request.Arguments.Contains("remove"));
    }

    [Fact]
    public async Task PartialCreationCancellationReportsRetainedDirectoryAndOriginalToken()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var taskId = Guid.NewGuid();
        var target = fixture.Target(taskId);
        var metadata = Path.Combine(fixture.Repository, ".git");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new StubTaskProcess { StandardOutput = new StringReader($"{fixture.Repository}\n{metadata}\n{metadata}\n{new string('a', 40)}\n") };
        var second = new StubTaskProcess
        {
            Wait = token =>
            {
                Directory.CreateDirectory(target);
                entered.SetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };
        var responses = new Queue<StubTaskProcess>([first, second]);
        var launcher = new RecordingTaskProcessLauncher(() => responses.Dequeue());
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        var execution = service.CreateAsync(fixture.Project, taskId, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await cancellation.CancelAsync();
        var failure = await Assert.ThrowsAsync<GitWorktreeCreationException>(() => execution);
        Assert.Equal(target, failure.WorktreePath);
        Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(Directory.Exists(target));
        Assert.True(second.Disposed);
        Assert.Equal(2, launcher.Requests.Count);
    }

    [Fact]
    public async Task AuthorizationIsCapturedBeforeReturningToCallerAndNeedsNoMessagePump()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var roots = new List<string> { fixture.Repository };
        var project = fixture.Project with { AuthorizedDirectories = roots };
        var context = new QueuedSynchronizationContext();
        var original = SynchronizationContext.Current;
        Task<string> execution;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            execution = fixture.Service.CreateAsync(project, Guid.NewGuid(), fixture.Token);
            roots.Clear();
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
        try
        {
            var target = await execution.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
            Assert.True(Directory.Exists(target));
            Assert.Equal(0, context.Posts);
            await fixture.Service.RemoveAsync(fixture.Project, target, fixture.Token);
        }
        finally { context.Drain(); }
    }
}
