using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectReconciliationTests
{
    [Fact]
    public void DuplicateReferences_AreRestoredWhenOneOccurrenceIsRemoved()
    {
        var first = CreateProject("first");
        var second = CreateProject("second");
        var source = new ObservableCollection<WorkspaceProjectNavigationViewModel> { first, first, second };
        using var list = new WorkspaceSidebarProjectList(source);
        var actions = Observe(list.Items);

        source.Remove(first);

        Assert.Equal([first, second], list.Items);
        Assert.Equal([NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Remove], actions);
        first.UpdateProject(first.Project with { Name = "third" });
        Assert.Equal([second, first], list.Items);
    }

    [Fact]
    public void DuplicateReferences_AreInsertedWithoutReplacingExistingRows()
    {
        var first = CreateProject("first");
        var second = CreateProject("second");
        var source = new ObservableCollection<WorkspaceProjectNavigationViewModel> { first, second };
        using var list = new WorkspaceSidebarProjectList(source);
        var actions = Observe(list.Items);

        source.Add(first);

        Assert.Equal([first, first, second], list.Items);
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
    }

    [Fact]
    public void SameOrder_DoesNotPublishCollectionChanges()
    {
        var first = CreateProject("first");
        var second = CreateProject("second");
        var source = new ObservableCollection<WorkspaceProjectNavigationViewModel> { first, second };
        using var list = new WorkspaceSidebarProjectList(source);
        var actions = Observe(list.Items);

        source.Move(0, 1);
        first.UpdateProject(first.Project with { });

        Assert.Equal([first, second], list.Items);
        Assert.Empty(actions);
    }

    [Fact]
    public void Clearing_RemovesAllOccurrencesWithoutReset()
    {
        var project = CreateProject("project");
        var source = new ObservableCollection<WorkspaceProjectNavigationViewModel> { project, project };
        using var list = new WorkspaceSidebarProjectList(source);
        var actions = Observe(list.Items);

        source.Clear();

        Assert.Empty(list.Items);
        Assert.Equal([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove], actions);
        project.UpdateProject(project.Project with { Name = "detached" });
        Assert.Empty(list.Items);
    }

    private static List<NotifyCollectionChangedAction> Observe(
        ObservableCollection<WorkspaceProjectNavigationViewModel> rows)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, args) => actions.Add(args.Action);
        return actions;
    }

    private static WorkspaceProjectNavigationViewModel CreateProject(string name) => new(new ProjectProfile(
        Guid.NewGuid(), name, "D:\\synthetic", [], DateTimeOffset.UnixEpoch));
}
