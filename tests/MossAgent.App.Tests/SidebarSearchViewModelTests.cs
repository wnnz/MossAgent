using System.ComponentModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarSearchViewModelTests
{
    [Fact]
    public void Matches_AggregateEveryProjectInsteadOfOnlyTheSelectedTaskList()
    {
        var catalog = Catalog();
        var group = SidebarProjectOrderingTests.Project("other project", 0);
        group.SetTasks([SidebarProjectOrderingTests.TaskFor(group, "matching conversation", 0)]);
        catalog.ProjectNavigation.Add(group);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "conversation";
        Assert.Empty(catalog.Tasks);
        Assert.True(search.HasMatches);
        search.Query = "other project";
        Assert.True(search.HasMatches);
        search.Query = "no matches";
        Assert.False(search.HasMatches);
        search.ClearCommand.Execute(null);
        Assert.Equal(string.Empty, catalog.TaskSearch.Query);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void PinnedOnlyMatchAndLiveRemoval_UpdateTheEmptyState()
    {
        var catalog = Catalog();
        var group = SidebarProjectOrderingTests.Project("project", 0);
        var task = SidebarProjectOrderingTests.TaskFor(group, "pinned keyword", 0) with { IsPinned = true };
        group.SetTasks([task]);
        catalog.ProjectNavigation.Add(group);
        catalog.TaskManagement.PinnedTasks.SetTasks([task], string.Empty);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "keyword";
        Assert.False(group.IsVisible);
        Assert.True(search.HasMatches);
        catalog.TaskManagement.PinnedTasks.Remove(task.Id);
        Assert.False(search.HasMatches);
        catalog.TaskManagement.PinnedTasks.Upsert(task);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void RecentOnlyMatch_UsesLiveResultsAndStopsMatchingAfterRemoval()
    {
        var catalog = Catalog();
        var group = SidebarProjectOrderingTests.Project("project", 0);
        var task = SidebarProjectOrderingTests.TaskFor(group, "recent keyword", 0);
        catalog.RecentTasks.SetTasks([task], string.Empty);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "keyword";
        Assert.True(search.HasMatches);
        catalog.RecentTasks.Remove(task.Id);
        Assert.False(search.HasMatches);
        catalog.RecentTasks.Upsert(task);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void ReplacedAndAddedGroups_AreObservedWithoutLosingTheQuery()
    {
        var catalog = Catalog();
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "keyword";
        Assert.False(search.HasMatches);
        var previous = SidebarProjectOrderingTests.Project("keyword", 0);
        previous.ApplyFilter(search.Query);
        catalog.ProjectNavigation.Add(previous);
        Assert.True(search.HasMatches);
        var replacement = SidebarProjectOrderingTests.Project("no match", 0);
        replacement.ApplyFilter(search.Query);
        catalog.ProjectNavigation[0] = replacement;
        Assert.False(search.HasMatches);
        replacement.UpdateProject(replacement.Project with { Name = "keyword" });
        Assert.True(search.HasMatches);
        catalog.ProjectNavigation.Clear();
        Assert.False(search.HasMatches);
        previous.ApplyFilter(string.Empty);
        Assert.False(search.HasMatches);
        Assert.Equal("keyword", search.Query);
    }

    [Fact]
    public void ClearAvailabilityAndDisposedCommands_RejectDirectLateMutations()
    {
        var catalog = Catalog();
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        var notifications = 0;
        search.ClearCommand.CanExecuteChanged += (_, _) => notifications++;
        Assert.False(search.ClearCommand.CanExecute(null));
        search.Query = "keyword";
        Assert.True(search.ClearCommand.CanExecute(null));
        Assert.True(notifications > 0);
        search.Dispose();
        Assert.False(search.ClearCommand.CanExecute(null));
        search.ClearCommand.Execute(null);
        search.Query = "late";
        Assert.Equal("keyword", catalog.TaskSearch.Query);
    }

    [Fact]
    public void Dispose_DetachesEveryResultSourceAndRejectsLatePublication()
    {
        var catalog = Catalog();
        var group = SidebarProjectOrderingTests.Project("project", 0);
        catalog.ProjectNavigation.Add(group);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "no match";
        var notifications = new List<string?>();
        search.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        search.Dispose();
        catalog.TaskSearch.Query = string.Empty;
        catalog.ProjectNavigation.Clear();
        catalog.ProjectNavigation.Add(SidebarProjectOrderingTests.Project("late", 1));
        catalog.RecentTasks.SetTasks([SidebarProjectOrderingTests.TaskFor(group, "late", 1)], string.Empty);
        Assert.Empty(notifications);
        Assert.False(search.HasMatches);
    }

    internal static WorkspaceCatalogViewModel Catalog()
    {
        var repository = new TestWorkspaceRepository();
        return new WorkspaceCatalogViewModel(repository, null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
    }
}
