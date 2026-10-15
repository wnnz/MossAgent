using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectPreferenceLifetimeTests
{
    [Fact]
    public async Task FailedRead_BlocksWritesUntilExplicitRecoveryAndHidesRawErrors()
    {
        var store = new TestSidebarPreferenceStore
        {
            LoadHandler = _ => throw new IOException("secret sentinel")
        };
        using var viewModel = new WorkspaceSidebarProjectsViewModel([], store);
        await viewModel.LoadAsync();
        Assert.True(viewModel.HasError);
        Assert.DoesNotContain("secret sentinel", viewModel.Status);
        Assert.False(viewModel.SortManuallyCommand.CanExecute(null));
        await viewModel.SortManuallyCommand.ExecuteAsync(null);
        Assert.Equal(0, store.Saves);
        store.LoadHandler = null;
        await viewModel.RetryLoadCommand.ExecuteAsync(null);
        Assert.False(viewModel.HasError);
        await viewModel.SortManuallyCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsManualSort);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task FailedSave_KeepsTheOldOrderAndAllowsRetry()
    {
        var store = new TestSidebarPreferenceStore { SaveHandler = (_, _) => throw new IOException("secret sentinel") };
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [
            SidebarProjectOrderingTests.Project("first", 1), SidebarProjectOrderingTests.Project("second", 0)];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        await viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        Assert.Equal(source, viewModel.Items);
        Assert.True(viewModel.IsActivitySort);
        Assert.True(viewModel.HasError);
        Assert.DoesNotContain("secret sentinel", viewModel.Status);
        store.SaveHandler = null;
        await viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        Assert.Equal([source[1], source[0]], viewModel.Items);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task NonCooperativeSave_RetainsOwnershipWhileTheCatalogChanges()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new TestSidebarPreferenceStore { SaveHandler = (_, _) => completed.Task };
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [
            SidebarProjectOrderingTests.Project("first", 1), SidebarProjectOrderingTests.Project("second", 0)];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var saving = viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        viewModel.MoveDownCommand.Cancel();
        Assert.True(viewModel.IsBusy);
        source.RemoveAt(1);
        source.Add(SidebarProjectOrderingTests.Project("added", 5));
        await viewModel.SortManuallyCommand.ExecuteAsync(null);
        await viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        Assert.Equal(1, store.Saves);
        completed.SetResult();
        await saving;
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.HasError);
        Assert.True(viewModel.IsActivitySort);
        Assert.Equal("added", viewModel.Items[0].Project.Name);
    }

    [Fact]
    public async Task Dispose_CancelsPendingReadAndRejectsLateResultsAndNotifications()
    {
        var completed = new TaskCompletionSource<SidebarPreference>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var store = new TestSidebarPreferenceStore
        {
            LoadHandler = token => { captured = token; return completed.Task; }
        };
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [SidebarProjectOrderingTests.Project("first", 1)];
        var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        var loading = viewModel.LoadAsync();
        Assert.False(viewModel.SortManuallyCommand.CanExecute(null));
        var items = viewModel.Items.ToArray();
        viewModel.Dispose();
        Assert.True(captured.IsCancellationRequested);
        source.Clear();
        source.Add(SidebarProjectOrderingTests.Project("late", 8));
        completed.SetResult(new SidebarPreference(SidebarProjectSortOrder.Manual, []));
        await loading;
        Assert.True(viewModel.IsActivitySort);
        Assert.Equal(items, viewModel.Items);
        Assert.False(viewModel.RetryLoadCommand.CanExecute(null));
        await viewModel.SortManuallyCommand.ExecuteAsync(null);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task SuccessfulSave_ReconcilesNewAndRemovedProjectsAgainstTheLiveCatalog()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new TestSidebarPreferenceStore { SaveHandler = (_, _) => completed.Task };
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [
            SidebarProjectOrderingTests.Project("first", 1), SidebarProjectOrderingTests.Project("second", 0)];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var second = source[1];
        var saving = viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        source.RemoveAt(0);
        var added = SidebarProjectOrderingTests.Project("added", 5);
        source.Add(added);
        completed.SetResult();
        await saving;
        Assert.Equal([second, added], viewModel.Items);
        Assert.True(viewModel.IsManualSort);
    }
}
