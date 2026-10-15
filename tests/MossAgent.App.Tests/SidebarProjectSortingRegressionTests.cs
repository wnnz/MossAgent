using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectSortingRegressionTests
{
    [Fact]
    public void ActivitySort_IgnoresSavedManualRanksAndMovesExistingRows()
    {
        var older = SidebarProjectOrderingTests.Project("older", 0);
        var newer = SidebarProjectOrderingTests.Project("newer", 1);
        older.IsExpanded = older.IsSelected = true;
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [older, newer];
        using var list = new WorkspaceSidebarProjectList(source);
        list.SetPreference(new(SidebarProjectSortOrder.UpdatedAt, [older.Project.Id, newer.Project.Id]));
        var changes = new List<NotifyCollectionChangedAction>();
        list.Items.CollectionChanged += (_, args) => changes.Add(args.Action);

        older.UpsertTask(SidebarProjectOrderingTests.TaskFor(older, "latest", 2));

        Assert.Equal([older, newer], list.Items);
        Assert.Equal([older, newer], source);
        Assert.Equal([NotifyCollectionChangedAction.Move], changes);
        Assert.True(older.IsExpanded);
        Assert.True(older.IsSelected);
    }

    [Fact]
    public void ManualSort_UsesSavedRanksThenCreationTimeForUnrankedProjects()
    {
        var ranked = SidebarProjectOrderingTests.Project("ranked", 0);
        var older = SidebarProjectOrderingTests.Project("older", 1);
        var newer = SidebarProjectOrderingTests.Project("newer", 2);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [older, newer, ranked];
        using var list = new WorkspaceSidebarProjectList(source);
        list.SetPreference(new(SidebarProjectSortOrder.Manual, [Guid.NewGuid(), ranked.Project.Id]));

        older.UpsertTask(SidebarProjectOrderingTests.TaskFor(older, "activity", 9));

        Assert.Equal([ranked, newer, older], list.Items);
        Assert.Equal([older, newer, ranked], source);
    }

    [Theory]
    [InlineData(SidebarProjectSortOrder.UpdatedAt)]
    [InlineData(SidebarProjectSortOrder.Manual)]
    public void TiedSortKeys_UseNameThenIdentityAndDoNotResetUnchangedRows(SidebarProjectSortOrder order)
    {
        var alpha = SidebarProjectOrderingTests.Project("alpha", 0);
        var first = SidebarProjectOrderingTests.Project("beta", 0);
        var second = SidebarProjectOrderingTests.Project("beta", 0);
        first.UpdateProject(first.Project with { Id = Guid.Parse("00000000-0000-0000-0000-000000000001") });
        second.UpdateProject(second.Project with { Id = Guid.Parse("00000000-0000-0000-0000-000000000002") });
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [second, first, alpha];
        using var list = new WorkspaceSidebarProjectList(source);
        var preference = new SidebarPreference(order, []);
        list.SetPreference(preference);
        var changes = 0;
        list.Items.CollectionChanged += (_, _) => changes++;

        list.SetPreference(preference);
        alpha.ApplyFilter("no match");

        Assert.Equal([alpha, first, second], list.Items);
        Assert.Equal(0, changes);
        Assert.False(alpha.IsVisible);
    }

    [Fact]
    public void RemovedRowsAndDisposedLists_StopRespondingToOldActivity()
    {
        var removed = SidebarProjectOrderingTests.Project("removed", 0);
        var remaining = SidebarProjectOrderingTests.Project("remaining", 1);
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [removed, remaining];
        using var list = new WorkspaceSidebarProjectList(source);
        source.Remove(removed);
        var changes = 0;
        list.Items.CollectionChanged += (_, _) => changes++;

        removed.UpsertTask(SidebarProjectOrderingTests.TaskFor(removed, "late", 5));
        list.Dispose();
        source.Add(removed);
        remaining.UpsertTask(SidebarProjectOrderingTests.TaskFor(remaining, "after dispose", 9));
        list.SetPreference(new(SidebarProjectSortOrder.Manual, [removed.Project.Id]));

        Assert.Same(remaining, Assert.Single(list.Items));
        Assert.Equal(0, changes);
    }
}
