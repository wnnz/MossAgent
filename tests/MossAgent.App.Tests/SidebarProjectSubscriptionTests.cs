using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectSubscriptionTests
{
    [Fact]
    public void ReplacementWithExistingProject_PreservesItsSubscriptionAfterRemovingOneOccurrence()
    {
        var outgoing = SidebarProjectOrderingTests.Project("outgoing", 0);
        var retained = SidebarProjectOrderingTests.Project("retained", 0);
        var other = SidebarProjectOrderingTests.Project("middle", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [outgoing, retained, other];
        using var list = new WorkspaceSidebarProjectList(source);

        source[0] = retained;
        retained.UpdateProject(retained.Project with { Name = "alpha" });
        Assert.Equal([retained, retained, other], list.Items);
        source.RemoveAt(0);
        retained.UpdateProject(retained.Project with { Name = "zulu" });
        Assert.Equal([other, retained], list.Items);
        var notifications = 0;
        list.Items.CollectionChanged += (_, _) => notifications++;

        outgoing.UpdateProject(outgoing.Project with { Name = "detached" });

        Assert.Equal(0, notifications);
        Assert.Equal([other, retained], list.Items);
    }

    [Fact]
    public void ReplacingOneDuplicate_KeepsTheOutgoingProjectObservedUntilItsFinalReplacement()
    {
        var outgoing = SidebarProjectOrderingTests.Project("outgoing", 0);
        var retained = SidebarProjectOrderingTests.Project("retained", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [outgoing, outgoing, retained];
        using var list = new WorkspaceSidebarProjectList(source);

        source[0] = retained;
        outgoing.UpdateProject(outgoing.Project with { Name = "zulu" });
        Assert.Equal([retained, retained, outgoing], list.Items);
        outgoing.UpdateProject(outgoing.Project with { Name = "alpha" });
        Assert.Equal([outgoing, retained, retained], list.Items);
        source[1] = retained;
        outgoing.UpdateProject(outgoing.Project with { Name = "detached" });

        Assert.Equal([retained, retained, retained], list.Items);
    }

    [Fact]
    public void MovingEqualSortKeys_PreservesStableSourceOrderAndBothSubscriptions()
    {
        var first = SidebarProjectOrderingTests.Project("same", 0);
        var second = new WorkspaceProjectNavigationViewModel(first.Project);
        first.IsExpanded = first.IsSelected = true;
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [first, second];
        using var list = new WorkspaceSidebarProjectList(source);

        source.Move(0, 1);
        Assert.Equal([second, first], list.Items);
        first.UpdateProject(first.Project with { Name = "alpha" });
        Assert.Equal([first, second], list.Items);
        second.UpdateProject(second.Project with { Name = "aardvark" });

        Assert.Equal([second, first], list.Items);
        Assert.Equal([second, first], source);
        Assert.True(first.IsExpanded);
        Assert.True(first.IsSelected);
    }

    [Fact]
    public void ClearingDuplicates_ObservesReinsertedProjectsAndIgnoresDetachedOnes()
    {
        var removed = SidebarProjectOrderingTests.Project("removed", 0);
        var retained = SidebarProjectOrderingTests.Project("retained", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [removed, removed, retained];
        using var list = new WorkspaceSidebarProjectList(source);

        source.Clear();
        source.Add(retained);
        source.Add(removed);
        retained.UpdateProject(retained.Project with { Name = "alpha" });
        Assert.Equal([retained, removed], list.Items);
        source.Remove(removed);
        removed.UpdateProject(removed.Project with { Name = "detached" });

        Assert.Same(retained, Assert.Single(list.Items));
    }

    [Fact]
    public void DisposingOneList_DoesNotDetachAnotherOwnersSubscriptions()
    {
        var first = SidebarProjectOrderingTests.Project("first", 0);
        var second = SidebarProjectOrderingTests.Project("second", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [first, second];
        using var disposed = new WorkspaceSidebarProjectList(source);
        using var active = new WorkspaceSidebarProjectList(source);
        disposed.Dispose();

        first.UpdateProject(first.Project with { Name = "zulu" });
        Assert.Equal([first, second], disposed.Items);
        Assert.Equal([second, first], active.Items);
        source.Clear();
        source.Add(second);
        source.Add(first);
        first.UpdateProject(first.Project with { Name = "alpha" });

        Assert.Equal([first, second], active.Items);
        Assert.Equal([first, second], disposed.Items);
    }

    [Fact]
    public void CollectionNotificationCapturedBeforeDisposal_DoesNotRefreshTheDisposedList()
    {
        var original = SidebarProjectOrderingTests.Project("original", 0);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [original];
        WorkspaceSidebarProjectList? captured = null;
        source.CollectionChanged += (_, _) => captured!.Dispose();
        using var list = new WorkspaceSidebarProjectList(source);
        captured = list;

        source.Add(SidebarProjectOrderingTests.Project("late", 1));

        Assert.Same(original, Assert.Single(list.Items));
    }

    [Fact]
    public void ProjectNotificationCapturedBeforeDisposal_DoesNotRefreshTheDisposedList()
    {
        var first = SidebarProjectOrderingTests.Project("first", 0);
        var second = SidebarProjectOrderingTests.Project("second", 0);
        WorkspaceSidebarProjectList? captured = null;
        first.PropertyChanged += (_, _) => captured!.Dispose();
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [first, second];
        using var list = new WorkspaceSidebarProjectList(source);
        captured = list;

        first.UpdateProject(first.Project with { Name = "zulu" });

        Assert.Equal([first, second], list.Items);
    }
}
