using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectOrderingTests
{
    [Fact]
    public void ActivitySort_UsesUnfilteredPinnedSnapshotsAndKeepsSourceUntouched()
    {
        var older = Project("older", 0);
        var newer = Project("newer", 1);
        var task = TaskFor(older, "hidden pinned activity", 5) with { IsPinned = true };
        older.SetTasks([task]);
        older.ApplyFilter("no match");
        newer.ApplyFilter("no match");
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [newer, older];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source);
        Assert.Equal([older, newer], viewModel.Items);
        Assert.Equal([newer, older], source);
        Assert.Empty(older.Tasks);
        Assert.Equal(task.UpdatedAt, older.LastActivityAt);
        older.RemoveTask(task.Id);
        Assert.Equal([newer, older], viewModel.Items);
    }

    [Fact]
    public void ActivityUpdates_MoveExistingRowsWithoutLosingSelectionExpansionOrSearch()
    {
        var first = Project("first", 1);
        var second = Project("second", 0);
        var task = TaskFor(second, "match", 0);
        second.SetTasks([task]);
        second.SetSelectedTask(task.Id);
        second.IsSelected = true;
        second.IsExpanded = true;
        second.ApplyFilter("match");
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [first, second];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source);
        second.UpsertTask(task with { UpdatedAt = task.UpdatedAt.AddDays(2) });
        Assert.Same(second, viewModel.Items[0]);
        Assert.True(second.IsSelected);
        Assert.True(second.IsExpanded);
        Assert.True(Assert.Single(second.Tasks).IsSelected);
        first.ApplyFilter("match");
        Assert.False(first.IsVisible);
        Assert.Equal([first, second], source);
    }

    [Fact]
    public async Task ManualMoves_SaveAndRestoreAcrossNewViewModelsAndCatalogReplacement()
    {
        var store = new TestSidebarPreferenceStore();
        var first = Project("first", 2);
        var second = Project("second", 1);
        var third = Project("third", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [third, second, first];
        using (var viewModel = new WorkspaceSidebarProjectsViewModel(source, store))
        {
            await viewModel.LoadAsync();
            await viewModel.MoveUpCommand.ExecuteAsync(second);
            Assert.Equal([second, first, third], viewModel.Items);
            Assert.True(viewModel.IsManualSort);
            first.UpsertTask(TaskFor(first, "activity must not change manual order", 9));
            Assert.Equal([second, first, third], viewModel.Items);
            source.Remove(third);
            Assert.Equal([second, first], viewModel.Items);
        }
        using var restarted = new WorkspaceSidebarProjectsViewModel(source, store);
        await restarted.LoadAsync();
        Assert.Equal([second, first], restarted.Items);
        var replacement = new WorkspaceProjectNavigationViewModel(second.Project);
        source[source.IndexOf(second)] = replacement;
        Assert.Equal([replacement, first], restarted.Items);
        source.Add(Project("added", 4));
        Assert.Equal([second.Project.Id, first.Project.Id], restarted.Items.Take(2).Select(item => item.Project.Id));
        Assert.Equal(2, store.Loads);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task SortModes_PreserveSavedManualOrderAndRejectOutOfRangeOrStaleTargets()
    {
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [Project("first", 1), Project("second", 0)];
        using var viewModel = new WorkspaceSidebarProjectsViewModel(source);
        var first = source[0];
        var second = source[1];
        await viewModel.MoveDownCommand.ExecuteAsync(first);
        await viewModel.SortByActivityCommand.ExecuteAsync(null);
        Assert.Equal([first, second], viewModel.Items);
        await viewModel.SortManuallyCommand.ExecuteAsync(null);
        Assert.Equal([second, first], viewModel.Items);
        Assert.False(viewModel.MoveUpCommand.CanExecute(second));
        Assert.False(viewModel.MoveDownCommand.CanExecute(first));
        Assert.False(viewModel.MoveUpCommand.CanExecute(Project("stale", 5)));
        await viewModel.MoveUpCommand.ExecuteAsync(second);
        await viewModel.MoveDownCommand.ExecuteAsync(first);
        Assert.Equal([second, first], viewModel.Items);
    }

    [Fact]
    public async Task Reload_KeepsExpandedGroupsSearchAndSelectedTask()
    {
        var first = Project("first", 0);
        var second = Project("second", 1);
        var firstTask = TaskFor(first, "match older", 0);
        var selectedTask = TaskFor(first, "match selected", 2);
        var repository = new TestWorkspaceRepository { ProjectItems = [first.Project, second.Project] };
        repository.TasksByProject[first.Project.Id] = [firstTask, selectedTask];
        var otherTask = TaskFor(second, "other project activity", 1);
        repository.TasksByProject[second.Project.Id] = [otherTask];
        var catalog = new WorkspaceCatalogViewModel(repository,
            null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        await catalog.ReloadAsync();
        catalog.SelectedTask = selectedTask;
        catalog.ProjectNavigation[1].IsExpanded = true;
        catalog.TaskSearch.Query = "match";
        await catalog.ReloadAsync();
        Assert.Equal(selectedTask.Id, catalog.SelectedTask?.Id);
        Assert.All(catalog.ProjectNavigation, group => Assert.True(group.IsExpanded));
        Assert.False(catalog.ProjectNavigation[1].IsVisible);
        Assert.Equal("match", catalog.TaskSearch.Query);
        Assert.Equal([selectedTask.Id, firstTask.Id], catalog.RecentTasks.Items.Select(row => row.Task.Id));
        catalog.TaskSearch.Query = "no matches";
        await catalog.ReloadAsync();
        Assert.Equal(selectedTask.Id, catalog.SelectedTask?.Id);
        Assert.Empty(catalog.Tasks);
        Assert.Empty(catalog.RecentTasks.Items);
        catalog.TaskSearch.Query = string.Empty;
        Assert.Equal([selectedTask.Id, otherTask.Id, firstTask.Id], catalog.RecentTasks.Items.Select(row => row.Task.Id));
        Assert.Equal(selectedTask.Id, Assert.Single(catalog.RecentTasks.Items, row => row.IsSelected).Task.Id);
        catalog.TaskManagement.Dispose();
        catalog.ProjectEditor.Dispose();
    }

    internal static WorkspaceProjectNavigationViewModel Project(string name, int day) => new(new ProjectProfile(
        Guid.NewGuid(), name, "D:\\Dev\\" + name, ["D:\\Dev\\" + name], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day)));

    internal static AgentTask TaskFor(WorkspaceProjectNavigationViewModel project, string title, int day) => new(
        Guid.NewGuid(), project.Project.Id, title, ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        project.Project.CreatedAt, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(day), null);
}
