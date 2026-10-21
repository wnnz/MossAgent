using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationMonitorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ProjectProfile Project() => new(Guid.NewGuid(), "lease", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);

    [Fact]
    public async Task NotificationRevokesOldLeasesOnlyAndCannotReactivateThem()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        await using var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        await using var next = await monitor.AcquireAsync(reader.Current, Token);
        changes.Publish(project.Id, 2);
        Assert.True(old.IsRevoked);
        Assert.True(old.Revocation.IsCancellationRequested);
        Assert.False(next.IsRevoked);
        changes.Publish(project.Id, 1);
        changes.Publish(Guid.NewGuid(), 3);
        Assert.True(old.IsRevoked);
        Assert.False(next.IsRevoked);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project, Token));
    }

    [Fact]
    public async Task ChangeBetweenInitialReadAndRegistrationIsRejected()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        reader.Read = (count, _) =>
        {
            if (count == 1)
            {
                reader.Current = project with { AuthorizationRevision = 2 };
                changes.Publish(project.Id, 2);
                return Task.FromResult<ProjectProfile?>(project);
            }
            return Task.FromResult(reader.Current);
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project, Token));
        Assert.Equal(2, reader.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PollingRevokesOnExternalChangeOrReaderFailure(bool failure)
    {
        var project = Project();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, new ProjectAuthorizationChanges());
        await using var lease = await monitor.AcquireAsync(project, Token);
        if (failure) reader.Read = (_, _) => throw new InvalidOperationException("synthetic secret must not escape");
        else reader.Current = project with { AuthorizationRevision = 2 };
        await Task.Delay(Timeout.Infinite, lease.Revocation).ContinueWith(_ => { }, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(lease.IsRevoked);
    }

    [Fact]
    public async Task MissingProjectAndMutatedSnapshotAreDeniedBeforeLeaseCreation()
    {
        var project = Project();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, new ProjectAuthorizationChanges());
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project with { AuthorizedDirectories = [] }, Token));
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project with { PrimaryDirectory = "other" }, Token));
        reader.Current = null;
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project, Token));
    }

    [Fact]
    public async Task UncooperativeReaderCannotPreventDeadlineRevocationOrLeaseDisposal()
    {
        var project = Project();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var polling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<ProjectProfile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var time = new ManualToolTimeProvider();
        reader.Read = (count, _) =>
        {
            if (count <= 2) return Task.FromResult<ProjectProfile?>(project);
            polling.TrySetResult();
            return pending.Task;
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, new ProjectAuthorizationChanges(), time);
        await using var lease = await monitor.AcquireAsync(project, Token);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(Token);
        waiting.CancelAfter(TimeSpan.FromSeconds(5));
        while (!polling.Task.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(10, waiting.Token);
        }
        time.Advance(TimeSpan.FromSeconds(5));
        try { await Task.Delay(Timeout.Infinite, lease.Revocation).WaitAsync(TimeSpan.FromSeconds(5), Token); }
        catch (OperationCanceledException) when (lease.IsRevoked) { }
        await lease.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(lease.IsRevoked);
        pending.TrySetResult(project);
    }

    [Fact]
    public async Task CancellationAndConcurrentDisposalReleaseLeaseAndTolerateBrokenObservers()
    {
        var project = Project();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var changes = new ProjectAuthorizationChanges();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitor.AcquireAsync(project, cancelled.Token));
        Assert.Equal(0, reader.Reads);
        var lease = await monitor.AcquireAsync(project, Token);
        using var registration = lease.Revocation.Register(() => throw new InvalidOperationException("broken callback"));
        changes.Publish(project.Id, 2);
        await Task.WhenAll(lease.DisposeAsync().AsTask(), lease.DisposeAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(lease.Revocation.IsCancellationRequested);
        await monitor.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => monitor.AcquireAsync(project, Token));
    }

    [Fact]
    public async Task ConcurrentMonitorDisposalWaitsForOutstandingRevocationCallbacks()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        await using var monitor = new ProjectAuthorizationMonitor(new ProjectAuthorizationReaderProbe(project), changes);
        await using var lease = await monitor.AcquireAsync(project, Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lease.Revocation.Register(() => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); });
        changes.Publish(project.Id, 2);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            var first = monitor.DisposeAsync().AsTask();
            var second = monitor.DisposeAsync().AsTask();
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.True(lease.Revocation.IsCancellationRequested);
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GuardRejectsTaskDirectoryAndRootTampering(int mutation)
    {
        var project = Project();
        await using var monitor = new ProjectAuthorizationMonitor(new ProjectAuthorizationReaderProbe(project), new ProjectAuthorizationChanges());
        var context = new ToolExecutionContext(Guid.NewGuid(), project.PrimaryDirectory, project.AuthorizedDirectories, ApprovalPolicy.FullAccess);
        var guard = new ProjectWorkspaceGuard(monitor, project, context.TaskId, context.WorkingDirectory, context.AuthorizedRoots);
        context = mutation switch
        {
            0 => context with { TaskId = Guid.NewGuid() },
            1 => context with { WorkingDirectory = "other" },
            _ => context with { AuthorizedRoots = [] }
        };
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => guard.AcquireAsync(context, Token));
    }
}
