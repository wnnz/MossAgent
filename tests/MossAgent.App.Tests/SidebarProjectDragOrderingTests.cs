using System.Collections.ObjectModel;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectDragOrderingTests
{
    [Theory]
    [InlineData(0, 2, false, "second,first,third")]
    [InlineData(0, 2, true, "second,third,first")]
    [InlineData(2, 0, false, "third,first,second")]
    [InlineData(2, 0, true, "first,third,second")]
    [InlineData(1, 2, true, "first,third,second")]
    [InlineData(1, 0, false, "second,first,third")]
    public async Task Drop_PersistsBothInsertionDirectionsAndRetainsRowState(
        int sourceIndex, int targetIndex, bool after, string expected)
    {
        var source = Projects();
        var store = new TestSidebarPreferenceStore();
        var dragged = source[sourceIndex];
        var target = source[targetIndex];
        dragged.IsExpanded = true;
        dragged.IsSelected = true;
        dragged.IsSummaryOpen = target.IsSummaryOpen = true;
        using (var viewModel = new WorkspaceSidebarProjectsViewModel(source, store))
        {
            await viewModel.LoadAsync();
            await viewModel.ReorderCommand.ExecuteAsync(Request(viewModel, dragged, target, after));
            Assert.Equal(expected.Split(','), viewModel.Items.Select(item => item.Project.Name));
            Assert.True(viewModel.IsManualSort);
            Assert.True(dragged.IsExpanded);
            Assert.True(dragged.IsSelected);
            Assert.False(dragged.IsSummaryOpen);
            Assert.False(target.IsSummaryOpen);
            Assert.Equal("first,second,third", string.Join(',', source.Select(item => item.Project.Name)));
            Assert.Equal(viewModel.Items.Select(item => item.Project.Id), store.Saved.ManualProjectOrder);
        }
        using var restarted = new WorkspaceSidebarProjectsViewModel(source, store);
        await restarted.LoadAsync();
        Assert.Equal(expected.Split(','), restarted.Items.Select(item => item.Project.Name));
        Assert.Equal(1, store.Saves);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, true)]
    public async Task ProbeAndUnchangedDrop_DoNotWriteOrChangeRowState(
        int sourceIndex, int targetIndex, bool after)
    {
        var source = Projects();
        var store = new TestSidebarPreferenceStore();
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var probe = Request(viewModel, source[0], source[0], false);
        Assert.True(viewModel.ReorderCommand.CanExecute(probe));
        await viewModel.ReorderCommand.ExecuteAsync(probe);
        var changes = 0;
        viewModel.Items.CollectionChanged += (_, _) => changes++;
        source[sourceIndex].IsSummaryOpen = source[targetIndex].IsSummaryOpen = true;
        await viewModel.ReorderCommand.ExecuteAsync(Request(viewModel, source[sourceIndex], source[targetIndex], after));
        Assert.Equal(0, store.Saves);
        Assert.Equal(0, changes);
        Assert.True(viewModel.IsActivitySort);
        Assert.Equal(source, viewModel.Items);
        Assert.True(source[sourceIndex].IsSummaryOpen);
        Assert.True(source[targetIndex].IsSummaryOpen);
    }

    [Fact]
    public async Task ForeignScopeAndStaleIdentities_AreRejectedEvenWithMatchingIds()
    {
        var source = Projects();
        var store = new TestSidebarPreferenceStore();
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var stale = source[0].Project;
        source[0] = new WorkspaceProjectNavigationViewModel(stale with { Name = "replaced" });
        AppProjectReorderRequest[] requests = [
            new(new object(), source[0].Project, source[1].Project, true),
            new(viewModel.ReorderScope, stale, source[1].Project, true),
            new(viewModel.ReorderScope, source[0].Project, stale, true),
            new(viewModel.ReorderScope, new object(), source[1].Project, true)];
        foreach (var request in requests)
        {
            Assert.False(viewModel.ReorderCommand.CanExecute(request));
            await viewModel.ReorderCommand.ExecuteAsync(request);
        }
        Assert.False(viewModel.ReorderCommand.CanExecute((AppProjectReorderRequest?)null));
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task RejectedReentry_DoesNotCancelTheOwnedSave()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var store = new TestSidebarPreferenceStore
        {
            SaveHandler = (_, token) => { captured = token; return completed.Task; }
        };
        var source = Projects();
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var request = Request(viewModel, source[0], source[2], true);
        var saving = viewModel.ReorderCommand.ExecuteAsync(request);
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.IsReorderAvailable);
        await viewModel.ReorderCommand.ExecuteAsync(request);
        viewModel.ReorderCommand.Execute(request);
        await viewModel.MoveDownCommand.ExecuteAsync(source[0]);
        Assert.False(captured.IsCancellationRequested);
        Assert.Equal(1, store.Saves);
        completed.SetResult();
        await saving;
        Assert.Equal([source[1], source[2], source[0]], viewModel.Items);
        Assert.False(viewModel.HasError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDispose_RejectsLateSaveWithoutReleasingOwnershipEarly(bool dispose)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var store = new TestSidebarPreferenceStore
        {
            SaveHandler = (_, token) => { captured = token; return completed.Task; }
        };
        var source = Projects();
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var saving = viewModel.ReorderCommand.ExecuteAsync(Request(viewModel, source[0], source[2], true));
        if (dispose) viewModel.Dispose();
        else viewModel.ReorderCommand.Cancel();
        Assert.True(captured.IsCancellationRequested);
        Assert.True(viewModel.IsBusy);
        Assert.False(saving.IsCompleted);
        completed.SetResult();
        await saving;
        Assert.Equal(source, viewModel.Items);
        Assert.True(viewModel.IsActivitySort);
        Assert.Equal(!dispose, viewModel.HasError);
        Assert.Equal(!dispose, viewModel.IsReorderAvailable);
    }

    [Fact]
    public async Task FailedSave_KeepsOrderAndAllowsExplicitRetryWithoutRawException()
    {
        var source = Projects();
        var store = new TestSidebarPreferenceStore { SaveHandler = (_, _) => throw new IOException("secret sentinel") };
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source, store);
        await viewModel.LoadAsync();
        var request = Request(viewModel, source[0], source[2], true);
        await viewModel.ReorderCommand.ExecuteAsync(request);
        Assert.Equal(source, viewModel.Items);
        Assert.True(viewModel.HasError);
        Assert.DoesNotContain("secret sentinel", viewModel.Status);
        store.SaveHandler = null;
        await viewModel.ReorderCommand.ExecuteAsync(request);
        Assert.Equal([source[1], source[2], source[0]], viewModel.Items);
        Assert.False(viewModel.HasError);
    }

    private static ObservableCollection<WorkspaceProjectNavigationViewModel> Projects() => [
        SidebarProjectOrderingTests.Project("first", 2), SidebarProjectOrderingTests.Project("second", 1),
        SidebarProjectOrderingTests.Project("third", 0)];

    private static AppProjectReorderRequest Request(WorkspaceSidebarProjectsViewModel viewModel,
        WorkspaceProjectNavigationViewModel source, WorkspaceProjectNavigationViewModel target, bool after) =>
        new(viewModel.ReorderScope, source.Project, target.Project, after);
}
