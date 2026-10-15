using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RepositoryStatusLifetimeTests
{
    [Fact]
    public async Task ServiceDisposal_CancelsAndWaitsForEveryOwnedRead()
    {
        using var probe = new RepositoryStatusProbe();
        await using var services = new ServiceCollection().AddSingleton<IGitRepositoryInfoService>(probe)
            .AddSingleton<WorkspaceRepositoryStatusViewModel>().BuildServiceProvider();
        var status = services.GetRequiredService<WorkspaceRepositoryStatusViewModel>();
        var first = status.RefreshAsync(Project("first"), null);
        var second = status.RefreshAsync(Project("second"), null);
        var changes = Observe(status);
        var stop = services.DisposeAsync().AsTask();
        try
        {
            Assert.All(probe.Reads, read => Assert.True(read.Token.IsCancellationRequested));
            Assert.False(stop.IsCompleted);
            probe.Reads[0].Completion.SetResult("stale-first");
            await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(stop.IsCompleted);
            probe.Reads[1].Completion.SetResult("stale-second");
            await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(second.IsCompletedSuccessfully);
            Assert.Empty(changes);
        }
        finally { probe.Dispose(); await stop; }
    }

    [Fact]
    public async Task RepeatedAsyncDisposal_RetainsOnePendingCompletion()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var refresh = status.RefreshAsync(Project("pending"), null);
        var lifetime = Assert.IsAssignableFrom<IAsyncDisposable>(status);
        var first = lifetime.DisposeAsync().AsTask();
        try
        {
            Assert.Same(first, lifetime.DisposeAsync().AsTask());
            Assert.False(first.IsCompleted);
            probe.Reads[0].Completion.SetResult("ignored");
            await first.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(refresh.IsCompletedSuccessfully);
        }
        finally { probe.Dispose(); await first; }
    }

    [Fact]
    public async Task DisposedOwner_RejectsNewReadsAndLateLabels()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var refresh = status.RefreshAsync(Project("pending"), null);
        var changes = Observe(status);
        Assert.IsAssignableFrom<IDisposable>(status).Dispose();
        Assert.True(probe.Reads[0].Token.IsCancellationRequested);
        await status.RefreshAsync(Project("rejected"), null);
        await status.RefreshAsync(null, null);
        Assert.Single(probe.Reads);
        probe.Reads[0].Completion.SetResult("ignored");
        await refresh.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Assert.IsAssignableFrom<IAsyncDisposable>(status).DisposeAsync();
        Assert.Empty(changes);
    }

    [Fact]
    public async Task SeparateOwner_StaysUsableWhenAnotherOwnerStops()
    {
        using var firstProbe = new RepositoryStatusProbe();
        using var secondProbe = new RepositoryStatusProbe();
        var first = new WorkspaceRepositoryStatusViewModel(firstProbe);
        var second = new WorkspaceRepositoryStatusViewModel(secondProbe);
        var firstRead = first.RefreshAsync(Project("first"), null);
        var secondRead = second.RefreshAsync(Project("second"), null);
        var stop = Assert.IsAssignableFrom<IAsyncDisposable>(first).DisposeAsync().AsTask();
        try
        {
            Assert.True(firstProbe.Reads[0].Token.IsCancellationRequested);
            Assert.False(secondProbe.Reads[0].Token.IsCancellationRequested);
            firstProbe.Reads[0].Completion.SetResult("ignored");
            secondProbe.Reads[0].Completion.SetResult("active");
            await Task.WhenAll(firstRead, secondRead, stop).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal("active", second.BranchLabel);
        }
        finally { firstProbe.Dispose(); secondProbe.Dispose(); await stop; }
    }

    [Fact]
    public async Task OlderResult_DoesNotOverwriteTheCurrentSelection()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var oldRead = status.RefreshAsync(Project("old"), null);
        var currentRead = status.RefreshAsync(Project("current"), null);
        probe.Reads[1].Completion.SetResult("current-branch");
        await currentRead;
        probe.Reads[0].Completion.SetResult("old-branch");
        await oldRead;
        Assert.Equal("current", status.DirectoryLabel);
        Assert.Equal("current-branch", status.BranchLabel);
    }

    [Fact]
    public async Task ClearingSelection_DoesNotAllowAPreviousResultToRestoreLabels()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var oldRead = status.RefreshAsync(Project("old"), null);
        await status.RefreshAsync(null, null);
        probe.Reads[0].Completion.SetResult("old-branch");
        await oldRead;
        Assert.Single(probe.Reads);
        Assert.Equal("未选择目录", status.DirectoryLabel);
        Assert.Equal("未选择仓库", status.BranchLabel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServiceFailures_UseSafeUnknownStateAndPermitAnotherRefresh(bool cancellationFailure)
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var failed = status.RefreshAsync(Project("failed"), null);
        probe.Reads[0].Completion.SetException(cancellationFailure
            ? new OperationCanceledException("fixture-secret-do-not-display")
            : new InvalidOperationException("fixture-secret-do-not-display"));
        await failed;
        Assert.Equal("Git 状态未知", status.BranchLabel);
        Assert.Equal("failed", status.DirectoryLabel);
        var recovery = status.RefreshAsync(Project("recovered"), null);
        probe.Reads[1].Completion.SetResult("main");
        await recovery;
        Assert.Equal("main", status.BranchLabel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_UsesTheSelectedWorktreeAndRetainsNonGitSemantics(bool worktree)
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var project = Project("project");
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "synthetic", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, worktree ? Path.Combine(Path.GetTempPath(), "worktree") : null);
        var refresh = status.RefreshAsync(project, task);
        var read = Assert.Single(probe.Reads);
        Assert.Equal(task.WorktreePath ?? project.PrimaryDirectory, read.Directory);
        Assert.True(read.Token.CanBeCanceled);
        read.Completion.SetResult(null);
        await refresh;
        Assert.Equal(worktree ? "worktree" : "project", status.DirectoryLabel);
        Assert.Equal("非 Git 仓库", status.BranchLabel);
    }

    [Fact]
    public async Task ThrowingCancellationObserver_DoesNotReleaseOwnershipEarly()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var refresh = status.RefreshAsync(Project("pending"), null);
        var read = Assert.Single(probe.Reads);
        using var registration = read.Token.Register(() => throw new InvalidOperationException("fixture-observer"));
        var stop = Assert.IsAssignableFrom<IAsyncDisposable>(status).DisposeAsync().AsTask();
        try
        {
            Assert.True(read.Token.IsCancellationRequested);
            Assert.False(stop.IsCompleted);
            read.Completion.SetResult("ignored");
            await Assert.ThrowsAsync<AggregateException>(() => stop);
            Assert.True(refresh.IsCompletedSuccessfully);
            Assert.Same(stop, Assert.IsAssignableFrom<IAsyncDisposable>(status).DisposeAsync().AsTask());
        }
        finally { probe.Dispose(); }
    }

    private static ProjectProfile Project(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), name);
        return new(Guid.NewGuid(), name, directory, [directory], DateTimeOffset.UtcNow);
    }

    private static List<string?> Observe(WorkspaceRepositoryStatusViewModel status)
    {
        var changes = new List<string?>();
        status.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }
}
