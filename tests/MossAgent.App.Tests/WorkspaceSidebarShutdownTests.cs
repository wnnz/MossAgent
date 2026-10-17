using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSidebarShutdownTests
{
    [Theory]
    [InlineData("load", false)]
    [InlineData("retry", false)]
    [InlineData("sort-manual", false)]
    [InlineData("sort-activity", false)]
    [InlineData("move-up", false)]
    [InlineData("move-down", false)]
    [InlineData("reorder", false)]
    [InlineData("load", true)]
    [InlineData("retry", true)]
    [InlineData("sort-manual", true)]
    [InlineData("sort-activity", true)]
    [InlineData("move-up", true)]
    [InlineData("move-down", true)]
    [InlineData("reorder", true)]
    public async Task ShutdownAndDisposal_DrainOwnedSidebarStorageBeforeCompleting(string kind, bool dispose)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        await using var probe = new SidebarStorageReleaseProbe();
        var store = CreateStore(kind, probe);
        var repository = new TestWorkspaceRepository
        {
            ProjectItems = [SidebarProjectOrderingTests.Project("first", 2).Project,
                SidebarProjectOrderingTests.Project("second", 1).Project, SidebarProjectOrderingTests.Project("third", 0).Project]
        };
        using var runs = new WorkspaceRunTracker();
        var workspace = fixture.CreateWorkspace(repository, runs, catalogRepository: repository, sidebarPreferences: store);
        try
        {
            await workspace.LoadAsync();
            var sidebar = workspace.SidebarProjects;
            if (kind == "sort-activity") await sidebar.SortManuallyCommand.ExecuteAsync(null);
            if (kind == "retry") store.LoadHandler = token => LoadBlockedAsync(probe, token);
            if (kind is not "load" and not "retry") store.SaveHandler = (_, token) => probe.BlockAsync(token);
            var pending = Start(sidebar, kind);
            await probe.Entered.Task.WaitAsync(fixture.Token);
            var original = sidebar.Items.ToArray();
            var saved = store.Saved;
            var status = sidebar.Status;
            var isManual = sidebar.IsManualSort;
            var stopping = dispose ? workspace.DisposeAsync().AsTask() : workspace.ShutdownAsync();
            var first = await Task.WhenAny(stopping, probe.Cancelled.Task).WaitAsync(fixture.Token);

            Assert.Same(probe.Cancelled.Task, first);
            Assert.False(stopping.IsCompleted);
            Assert.True(probe.Token.IsCancellationRequested);
            Assert.Throws<IOException>(() => { using var competing = File.Open(probe.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            AssertCommandsBlocked(sidebar);
            var notifications = 0;
            sidebar.PropertyChanged += (_, _) => notifications++;
            sidebar.Items.CollectionChanged += (_, _) => notifications++;
            workspace.Catalog.ProjectNavigation.Clear();
            probe.Release.TrySetResult();
            await stopping.WaitAsync(fixture.Token);
            await pending.WaitAsync(fixture.Token);

            using var released = File.Open(probe.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(0, notifications);
            Assert.Equal(original, sidebar.Items);
            Assert.Equal(saved, store.Saved);
            Assert.Equal(status, sidebar.Status);
            Assert.Equal(isManual, sidebar.IsManualSort);
            Assert.Empty(fixture.Server.Requests);
        }
        finally
        {
            probe.Release.TrySetResult();
            await workspace.DisposeAsync();
        }
    }

    private static TestSidebarPreferenceStore CreateStore(string kind, SidebarStorageReleaseProbe probe) => new()
    {
        LoadHandler = kind == "load" ? token => LoadBlockedAsync(probe, token)
            : kind == "retry" ? _ => throw new IOException("synthetic-secret") : null
    };

    private static async Task<SidebarPreference> LoadBlockedAsync(SidebarStorageReleaseProbe probe, CancellationToken token)
    {
        await probe.BlockAsync(token);
        return new(SidebarProjectSortOrder.Manual, []);
    }

    private static Task Start(WorkspaceSidebarProjectsViewModel sidebar, string kind) => kind switch
    {
        "load" => sidebar.LoadAsync(),
        "retry" => sidebar.RetryLoadCommand.ExecuteAsync(null),
        "sort-manual" => sidebar.SortManuallyCommand.ExecuteAsync(null),
        "sort-activity" => sidebar.SortByActivityCommand.ExecuteAsync(null),
        "move-up" => sidebar.MoveUpCommand.ExecuteAsync(sidebar.Items[1]),
        "move-down" => sidebar.MoveDownCommand.ExecuteAsync(sidebar.Items[0]),
        "reorder" => sidebar.ReorderCommand.ExecuteAsync(new AppProjectReorderRequest(
            sidebar.ReorderScope, sidebar.Items[0].Project, sidebar.Items[1].Project, true)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void AssertCommandsBlocked(WorkspaceSidebarProjectsViewModel sidebar)
    {
        Assert.False(sidebar.SortManuallyCommand.CanExecute(null));
        Assert.False(sidebar.SortByActivityCommand.CanExecute(null));
        Assert.False(sidebar.RetryLoadCommand.CanExecute(null));
        Assert.False(sidebar.MoveUpCommand.CanExecute(sidebar.Items.FirstOrDefault()));
        Assert.False(sidebar.MoveDownCommand.CanExecute(sidebar.Items.FirstOrDefault()));
        Assert.False(sidebar.ReorderCommand.CanExecute(null));
    }
}
