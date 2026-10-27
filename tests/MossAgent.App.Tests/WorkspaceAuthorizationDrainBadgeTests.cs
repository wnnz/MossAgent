using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAuthorizationDrainBadgeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProjectRowBadgeFollowsRevokeAndReleaseLifecycle()
    {
        var project = new ProjectProfile(Guid.NewGuid(), "授权项目", Path.GetTempPath(),
            [Path.GetTempPath()], DateTimeOffset.UtcNow);
        var repository = new TestWorkspaceRepository { ProjectItems = [project] };
        var changes = new ProjectAuthorizationChanges();
        await using var monitor = new ProjectAuthorizationMonitor(
            new WorkspaceProjectAuthorizationReader(repository), changes);
        var catalog = Catalog(repository, monitor);
        await catalog.ReloadAsync();
        var row = Assert.Single(catalog.ProjectNavigation);
        Assert.False(row.HasDrainBadge);
        Assert.Equal(string.Empty, row.DrainLabel);

        var lease = await monitor.AcquireAsync(project, Token);
        repository.ProjectItems = [project with { AuthorizationRevision = 2 }];
        changes.Publish(project.Id, 2);
        await WaitForAsync(() => row.HasDrainBadge);
        Assert.Equal(AuthorizationDrainState.Draining, row.AuthorizationDrain);
        Assert.Equal(SidebarText.Get("ProjectDrainActive"), row.DrainLabel);

        await lease.DisposeAsync();
        await WaitForAsync(() => !row.HasDrainBadge);
        Assert.Equal(AuthorizationDrainState.Idle, row.AuthorizationDrain);
        Assert.Equal(string.Empty, row.DrainLabel);
    }

    [Fact]
    public async Task ProjectRowBadgeShowsUnknownWhenReleaseFails()
    {
        var path = Path.GetTempPath();
        var identity = new DirectoryObjectIdentity(path, "synthetic-volume", Guid.NewGuid().ToString("N"));
        var project = new ProjectProfile(Guid.NewGuid(), "授权项目", path, [path], DateTimeOffset.UtcNow)
            with { RootAuthorizations = [new ProjectRootAuthorization(Guid.NewGuid(), identity)] };
        var repository = new TestWorkspaceRepository { ProjectItems = [project] };
        var changes = new ProjectAuthorizationChanges();
        var monitor = new ProjectAuthorizationMonitor(
            new WorkspaceProjectAuthorizationReader(repository), changes,
            roots: new DrainBadgeRootFactory([identity]));
        var catalog = Catalog(repository, monitor);
        await catalog.ReloadAsync();
        var row = Assert.Single(catalog.ProjectNavigation);

        var lease = await monitor.AcquireAsync(project, Token);
        repository.ProjectItems = [project with { AuthorizationRevision = 2 }];
        changes.Publish(project.Id, 2);
        await Assert.ThrowsAnyAsync<Exception>(() => lease.DisposeAsync().AsTask());
        await WaitForAsync(() => row.AuthorizationDrain == AuthorizationDrainState.CleanupUnknown);
        Assert.True(row.HasDrainBadge);
        Assert.Equal(SidebarText.Get("ProjectDrainUnknown"), row.DrainLabel);
        await Assert.ThrowsAnyAsync<Exception>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task NonIdleRowSchedulesBoundedFollowupObservesUntilIdle()
    {
        var project = new ProjectProfile(Guid.NewGuid(), "授权项目", Path.GetTempPath(),
            [Path.GetTempPath()], DateTimeOffset.UtcNow);
        var repository = new TestWorkspaceRepository { ProjectItems = [project] };
        var changes = new ProjectAuthorizationChanges();
        var board = new CountingProjectDrainStateBoard { State = AuthorizationDrainState.Draining };
        var time = new ManualToolTimeProvider();
        await using var monitor = new ProjectAuthorizationMonitor(
            new WorkspaceProjectAuthorizationReader(repository), changes, time, board: board);
        var row = new WorkspaceProjectNavigationViewModel(project);
        using var tracker = new WorkspaceAuthorizationDrainTracker(monitor, _ => row, time);

        tracker.Refresh(row);
        await WaitForAsync(() => board.Aggregates >= 1);
        await WaitForAsync(() => row.AuthorizationDrain == AuthorizationDrainState.Draining);

        time.Advance(TimeSpan.FromSeconds(30));
        await WaitForAsync(() => board.Aggregates >= 2);
        Assert.Equal(2, board.Aggregates);
        time.Advance(TimeSpan.FromSeconds(30));
        await WaitForAsync(() => board.Aggregates >= 3);
        Assert.Equal(3, board.Aggregates);

        board.State = AuthorizationDrainState.Idle;
        time.Advance(TimeSpan.FromSeconds(30));
        await WaitForAsync(() => board.Aggregates >= 4);
        await WaitForAsync(() => row.AuthorizationDrain == AuthorizationDrainState.Idle);
        time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50, Token);
        Assert.Equal(4, board.Aggregates);
    }

    [Fact]
    public async Task IdleObservationSchedulesNoFollowup()
    {
        var project = new ProjectProfile(Guid.NewGuid(), "授权项目", Path.GetTempPath(),
            [Path.GetTempPath()], DateTimeOffset.UtcNow);
        var repository = new TestWorkspaceRepository { ProjectItems = [project] };
        var changes = new ProjectAuthorizationChanges();
        var board = new CountingProjectDrainStateBoard { State = AuthorizationDrainState.Idle };
        var time = new ManualToolTimeProvider();
        await using var monitor = new ProjectAuthorizationMonitor(
            new WorkspaceProjectAuthorizationReader(repository), changes, time, board: board);
        var row = new WorkspaceProjectNavigationViewModel(project);
        using var tracker = new WorkspaceAuthorizationDrainTracker(monitor, _ => row, time);

        tracker.Refresh(row);
        await WaitForAsync(() => board.Aggregates >= 1);
        time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50, Token);
        Assert.Equal(1, board.Aggregates);
        Assert.Equal(AuthorizationDrainState.Idle, row.AuthorizationDrain);
    }

    private static WorkspaceCatalogViewModel Catalog(TestWorkspaceRepository repository,
        ProjectAuthorizationMonitor monitor) => new(repository, null,
        new WorkspaceProjectService(repository, new TestFolderPickerService()), authorizations: monitor);

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(15, deadline.Token);
    }
}
