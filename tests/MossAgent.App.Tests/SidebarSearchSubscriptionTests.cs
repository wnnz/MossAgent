using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarSearchSubscriptionTests
{
    [Fact]
    public void DuplicateGroup_RemainsObservedUntilItsLastOccurrenceIsRemoved()
    {
        var catalog = SidebarSearchViewModelTests.Catalog();
        var group = SidebarProjectOrderingTests.Project("project", 0);
        catalog.ProjectNavigation.Add(group);
        catalog.ProjectNavigation.Add(group);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);

        catalog.ProjectNavigation.RemoveAt(0);
        group.ApplyFilter("missing");
        Assert.False(search.HasMatches);
        group.ApplyFilter(string.Empty);
        Assert.True(search.HasMatches);
        catalog.ProjectNavigation.RemoveAt(0);
        Assert.False(search.HasMatches);
        group.ApplyFilter("missing");
        group.ApplyFilter(string.Empty);
        Assert.False(search.HasMatches);
    }

    [Fact]
    public void ReplacementWithAnExistingGroup_DetachesOnlyTheOutgoingGroup()
    {
        var catalog = SidebarSearchViewModelTests.Catalog();
        var outgoing = SidebarProjectOrderingTests.Project("outgoing", 0);
        var retained = SidebarProjectOrderingTests.Project("retained", 1);
        retained.ApplyFilter("missing");
        catalog.ProjectNavigation.Add(outgoing);
        catalog.ProjectNavigation.Add(retained);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);

        catalog.ProjectNavigation[0] = retained;
        Assert.False(search.HasMatches);
        outgoing.ApplyFilter("missing");
        outgoing.ApplyFilter(string.Empty);
        Assert.False(search.HasMatches);
        catalog.ProjectNavigation.RemoveAt(1);
        retained.ApplyFilter(string.Empty);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void Move_KeepsBothGroupsObservedWithoutChangingTheQueryOrEmptyState()
    {
        var catalog = SidebarSearchViewModelTests.Catalog();
        var first = SidebarProjectOrderingTests.Project("first", 0);
        var second = SidebarProjectOrderingTests.Project("second", 1);
        catalog.ProjectNavigation.Add(first);
        catalog.ProjectNavigation.Add(second);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);
        search.Query = "matching";
        var notifications = new List<string?>();
        search.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        catalog.ProjectNavigation.Move(0, 1);
        Assert.Empty(notifications);
        Assert.Equal("matching", search.Query);
        Assert.False(search.HasMatches);
        first.ApplyFilter(string.Empty);
        Assert.True(search.HasMatches);
        first.ApplyFilter("matching");
        Assert.False(search.HasMatches);
        second.ApplyFilter(string.Empty);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void Reset_DetachesDuplicateGroupsAndObservesSubsequentInsertions()
    {
        var catalog = SidebarSearchViewModelTests.Catalog();
        var previous = SidebarProjectOrderingTests.Project("previous", 0);
        catalog.ProjectNavigation.Add(previous);
        catalog.ProjectNavigation.Add(previous);
        using var search = new WorkspaceSidebarSearchViewModel(catalog);

        catalog.ProjectNavigation.Clear();
        previous.ApplyFilter("missing");
        previous.ApplyFilter(string.Empty);
        Assert.False(search.HasMatches);
        var added = SidebarProjectOrderingTests.Project("added", 1);
        added.ApplyFilter("missing");
        catalog.ProjectNavigation.Add(added);
        Assert.False(search.HasMatches);
        added.ApplyFilter(string.Empty);
        Assert.True(search.HasMatches);
    }

    [Fact]
    public void DisposingOneSearch_DoesNotDetachAnotherOwnersSubscriptions()
    {
        var catalog = SidebarSearchViewModelTests.Catalog();
        var group = SidebarProjectOrderingTests.Project("project", 0);
        catalog.ProjectNavigation.Add(group);
        using var first = new WorkspaceSidebarSearchViewModel(catalog);
        using var second = new WorkspaceSidebarSearchViewModel(catalog);
        first.Dispose();

        group.ApplyFilter("missing");
        Assert.True(first.HasMatches);
        Assert.False(second.HasMatches);
        catalog.ProjectNavigation.Clear();
        catalog.ProjectNavigation.Add(SidebarProjectOrderingTests.Project("new project", 1));
        Assert.True(second.HasMatches);
        second.Dispose();
        catalog.ProjectNavigation.Clear();
        Assert.True(second.HasMatches);
    }
}
